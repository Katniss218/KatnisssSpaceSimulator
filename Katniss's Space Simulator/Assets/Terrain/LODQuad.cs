﻿using KatnisssSpaceSimulator.Core;
using KatnisssSpaceSimulator.Core.ReferenceFrames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

namespace KatnisssSpaceSimulator.Terrain
{
    /// <summary>
    /// A subdivisible inflated quad.
    /// </summary>
    /// <remarks>
    /// This class does all of the heavy lifting of the system. <see cref="LODQuadSphere"/> only manages the 6 quads and tells them where the POIs are.
    /// </remarks>
    [RequireComponent( typeof( MeshFilter ) )]
    [RequireComponent( typeof( MeshCollider ) )]
    [RequireComponent( typeof( MeshRenderer ) )]
    public class LODQuad : MonoBehaviour
    {
        public abstract class State
        {
            public class Idle : State
            {

            }
            public class Active : State
            {

            }
            public class Rebuild : State
            {
                public MakeQuadMesh_Job Job;
                public JobHandle JobHandle;
            }
        }

        public LODQuadTree.Node Node { get; private set; }

        /// <summary>
        /// How many edge subdivision steps to apply for each quad, on top of and regardless of <see cref="SubdivisionLevel"/>.
        /// </summary>
        public int EdgeSubdivisions { get => _quadSphere.EdgeSubdivisions; }

        public CelestialBody CelestialBody { get; private set; }

        public float SubdivisionDistance { get; private set; }

        public Vector3Dbl[] AirfPOIs { get; set; }

        [field: SerializeField]
        public int SubdivisionLevel { get; private set; }

        State _currentState;
        State _nextState;

        [SerializeField]
        bool currentlySubdividing = false; // temp debugging

        Direction3D _quadSphereFace;

        MeshFilter _meshFilter;
        MeshCollider _meshCollider;
        MeshRenderer _meshRenderer;

        LODQuadSphere _quadSphere;

        [SerializeField]
        public LODQuad[] Edges = new LODQuad[4];

        void Awake()
        {
            _meshFilter = this.GetComponent<MeshFilter>();
            _meshCollider = this.GetComponent<MeshCollider>();
            _meshRenderer = this.GetComponent<MeshRenderer>();
        }

        void Start()
        {
            if( this._currentState is State.Idle )
            {
                GenerateMeshData();
            }
        }

        void Update()
        {
            this.AirfPOIs = new Vector3Dbl[] { VesselManager.ActiveVessel.AIRFPosition };

            if( _currentState is State.Idle )
            {
                return;
            }

            if( _currentState is State.Active )
            {
                if( ShouldSubdivide() )
                {
                    Subdivide();
                    return;
                }

                if( ShouldUnsubdivide() )
                {
                    Unsubdivide();
                    return;
                }
            }
        }

        void LateUpdate()
        {
            UpdateState();
        }

        void SetCurrentState( State state )
        {
            if( this._currentState is State.Rebuild r )
            {
                r.JobHandle.Complete();
                r.Job.Finish( this );
            }

            if( state is State.Rebuild rebuild )
            {
                rebuild.Job.Initialize( this ); // This (and collection) would have to be Reflection-ified to make it extendable by other user-provided assemblies.
                rebuild.JobHandle = rebuild.Job.Schedule();
            }
            this._currentState = state;

            currentlySubdividing = _currentState is State.Rebuild;
        }

        public void SetState( State newState )
        {
            if( newState == null )
            {
                throw new ArgumentNullException( $"The new state can't be null." );
            }

            if( this._currentState is State.Rebuild )
            {
                // If it rebuilding, we need to wait for the next frame to update the state.
                this._nextState = newState;
            }
            else
            {
                SetCurrentState( newState );
            }
        }

        private void UpdateState()
        {
            if( this._nextState == null && !(this._currentState is State.Rebuild) )
            {
                return;
            }

            if( this._nextState == null )
            {
                this.SetCurrentState( new State.Active() );
                return;
            }

            SetCurrentState( this._nextState );
            this._nextState = null;
        }

        internal void SetMesh( Mesh mesh )
        {
            this._meshCollider.sharedMesh = mesh; // this is kinda slow.
            this._meshFilter.sharedMesh = mesh;
        }

        private bool ShouldSubdivide()
        {
            if( this.SubdivisionLevel >= _quadSphere.HardLimitSubdivLevel )
            {
                return false;
            }

            if( AirfPOIs == null )
            {
                return false;
            }

            if( AirfPOIs.Length == 0 )
            {
                return false;
            }

            // Check if any of the PIOs is within the subdiv radius.
            foreach( var airfPOI in this.AirfPOIs )
            {
                Vector3Dbl airfQuad = SceneReferenceFrameManager.SceneReferenceFrame.TransformPosition( this.transform.position );
                double dist = (airfPOI - airfQuad).magnitude;

                if( (float)dist < SubdivisionDistance )
                {
                    return true;
                }
            }
            return false;
        }

        private bool ShouldUnsubdivide()
        {
            if( this.SubdivisionLevel == 0 )
            {
                return false;
            }

            if( AirfPOIs == null ) // default.
            {
                return false;
            }

            if( AirfPOIs.Length == 0 )
            {
                return true;
            }

            foreach( var siblingNode in this.Node.Siblings )
            {
                // Don't unsubdivide if one of the siblings is subdivided. That would require handling nested unsubdivisions, and is nasty, blergh and unnecessary.
                if( siblingNode.Children != null )
                {
                    return false;
                }
                // Sibling node is still generating - don't unsubdivide.
                // Not having this can lead to memory leaks with jobs due to destroyed job handles not freeing their stuff.
                if( siblingNode.Value != null && siblingNode.Value._currentState is State.Rebuild )
                {
                    return false;
                }
            }

            // If the parent won't want to immediately subdivide again, unsubdivide.
            Vector3 originBodySpace = _quadSphereFace.GetSpherePoint( this.Node.Parent.Center ) * (float)CelestialBody.Radius;
            foreach( var airfPOI in this.AirfPOIs )
            {
                Vector3Dbl parentQuadOriginAirf = SceneReferenceFrameManager.SceneReferenceFrame.TransformPosition( this.CelestialBody.transform.TransformPoint( originBodySpace ) );
                double distanceToPoi = (airfPOI - parentQuadOriginAirf).magnitude;

                if( distanceToPoi > this.SubdivisionDistance * 2 ) // times 2 because parent subdiv range is 2x more than its child.
                {
                    return true;
                }
            }

            return false;
        }

        static readonly float Cos45DegPlusEpsilon = Mathf.Cos( 45 * Mathf.Deg2Rad ) + 0.025f; // also, cos(45deg) is equal to sin(45deg)

        public static List<LODQuad> UpdateNeighbors( IEnumerable<LODQuad> neighborQuads, LODQuad newQuad, Direction2D direction )
        {
            List<LODQuad> neighborsInSpecifiedDirection = new List<LODQuad>();

            // Find all nodes that lay in a given direction from the current node.
            foreach( var potentialQuad in neighborQuads )
            {
                if( potentialQuad == newQuad )
                    continue;

                Vector2 toNode = potentialQuad.Node.Center - newQuad.Node.Center;

                // Comparing with Vector2.zero for near-zero vectors leads to rounding errors.
                if( toNode.x == 0.0f && toNode.y == 0.0f )
                    continue;

                toNode /= toNode.magnitude; // Using Normalize() on near-zero vectors results in (0,0) instead of the correct vector.

                // if vector's principal axis points towards direction.
                float dot = Vector2.Dot( toNode, direction.ToVector2() );
                if( dot > Cos45DegPlusEpsilon )
                {
                    neighborsInSpecifiedDirection.Add( potentialQuad );
                }
            }

            Direction2D inverseDirection = direction.Inverse();

            List<LODQuad> updatedNeighbors = new List<LODQuad>();

            // Always update the neighbors' edges - in case the current node was unsubdivided, and the edges of all its contacting neighbors need to be updated.
            foreach( var neighbor in neighborsInSpecifiedDirection )
            {
                if( newQuad.SubdivisionLevel > neighbor.SubdivisionLevel ) // equivalent to checking whether there are multiple quads contacting the neighbor.
                {
                    continue;
                }

                // Return the neighbors that have changed.
                if( neighbor.Edges[(int)inverseDirection] != newQuad )
                {
                    neighbor.Edges[(int)inverseDirection] = newQuad;
                    updatedNeighbors.Add( neighbor );
                }
            }

            if( neighborsInSpecifiedDirection.Count != 1 )
            {
                // Don't update the current node if there are multiple (smaller) nodes contacting it from a given direction.
                // - Otherwise, possibly `unsubdividedQuad.neighbors[direction] = mixed;`. Maybe mark the edge with highest, lowest, or maybe mark each interval. idk.
                return updatedNeighbors;
            }

            newQuad.Edges[(int)direction] = neighborsInSpecifiedDirection[0];
            return updatedNeighbors;
        }

        /// <summary>
        /// Splits the quad into 4 separate quads.
        /// </summary>
        private void Subdivide()
        {
            if( this.Node.Children != null )
            {
                throw new InvalidOperationException( "Tried subdividing a subdivided node" );
            }

            if( this.SubdivisionLevel >= this._quadSphere.HardLimitSubdivLevel )
            {
                return;
            }

            LODQuadTree.Node rootNode = this.Node.Root;
            int newSubdivisionLevel = this.SubdivisionLevel + 1;

            this.Destroy();
            // Don't make the parent a leaf, because once subdivided there definitely are children.

            // Create the 4 nodes.
            LODQuad[] _4_quads = new LODQuad[4];

            for( int i = 0; i < 4; i++ )
            {
                (int xIndex, int yIndex) = LODQuadTree_NodeUtils.GetChildIndex( i );

                Vector2 subdividedCenter = LODQuadTree_NodeUtils.GetChildCenter( this.Node, xIndex, yIndex );
                Vector3 subdividedOrigin = this._quadSphereFace.GetSpherePoint( subdividedCenter.x, subdividedCenter.y ) * (float)this.CelestialBody.Radius;

                LODQuadTree.Node newNode = new LODQuadTree.Node( this.Node, subdividedCenter, LODQuadTree_NodeUtils.GetSize( newSubdivisionLevel ) );

                LODQuad newQuad = Create( this.transform.parent, subdividedOrigin, this._quadSphere, this.CelestialBody, subdividedCenter, newSubdivisionLevel, newNode, this.SubdivisionDistance / 2f, this._meshRenderer.material, this._quadSphereFace );
                _4_quads[i] = newQuad;
            }

            var updatedNeighbors = new List<LODQuad>();
            // Update neighbors.
            foreach( var quad in _4_quads )
            {
                // Query area of each node separately
                // - because if the entire area is queried, then the nodes that are not direct neighbors of the current node are included and it breaks shit.
                List<LODQuadTree.Node> queryResult = rootNode.QueryOverlappingLeaves( quad.Node.minX, quad.Node.minY, quad.Node.maxX, quad.Node.maxY );

                foreach( var direction in Direction2DUtils.Every )
                {
                    var newN = UpdateNeighbors( queryResult.Where( n => n.Value != null ).Select( n => n.Value ), quad, direction );
                    updatedNeighbors.AddRange( newN );
                }
                quad.GenerateMeshData();
            }

#warning TODO - When moving at high speeds relative to quad size, there is desync in the mesh.
            // at least I think this is what happens.

            // a quad was freshely created, its mesh was updated to the correct one, but its IsSubdividing was still true (maybe another subdiv started as next state?).
            // then on the next frame, its neighbor subdivided, and updated its edges, but since the quad was already subdivided, the resubdiv was moved to the next state.


            // Realistically, the proper way to do this would be to ensure that all the edges of every quad are resolved, and then call for rebuild of all quads that had their edges changed.

            // and then we won't need to store the next state. everything should be done neatly all at once, after the subdivs/unsubdivs are fully resolved, then the meshing starts, and ends in lateupdate on the same frame.


            foreach( var n in updatedNeighbors.Distinct() )
            {
                n.GenerateMeshData();
            }
        }

        /// <summary>
        /// Joins the quad and its 3 siblings into a single quad of lower subdivision level.
        /// </summary>
        private void Unsubdivide()
        {
            if( this.SubdivisionLevel <= 0 )
            {
                Debug.LogWarning( "Tried subdividing an l0 node" );
                return;
            }

            foreach( var qSibling in this.Node.Siblings )
            {
                if( qSibling.Children != null )
                {
                    return; // one of the siblings is subdivided
                }
            }

            // cache before removing the node from the quadtree.
            LODQuadTree.Node rootNode = this.Node.Root;
            LODQuadTree.Node parentNode = this.Node.Parent;

            foreach( var qSibling in this.Node.Siblings )
            {
                qSibling.Value.Destroy();
            }
            // Make the parent a leaf, because once unsubdivided, there will be no children. This should also remove it completely from the quadtree.
            this.Node.Parent.MakeLeaf();

            // create new node.
            Vector2 unsubdividedCenter = parentNode.Center;
            Vector3 unsubdividedOrigin = this._quadSphereFace.GetSpherePoint( unsubdividedCenter.x, unsubdividedCenter.y ) * (float)this.CelestialBody.Radius;
            int newSubdivisionLevel = this.SubdivisionLevel - 1;

            LODQuad newQuad = Create( this.transform.parent, unsubdividedOrigin, this._quadSphere, this.CelestialBody, unsubdividedCenter, newSubdivisionLevel, parentNode, this.SubdivisionDistance * 2f, this._meshRenderer.material, this._quadSphereFace );

            // update neighbors.
            List<LODQuadTree.Node> queryResult = rootNode.QueryOverlappingLeaves( newQuad.Node.minX, newQuad.Node.minY, newQuad.Node.maxX, newQuad.Node.maxY );

            var updatedNeighbors = new List<LODQuad>();
            foreach( var direction in Direction2DUtils.Every )
            {
                var newN = UpdateNeighbors( queryResult.Where( n => n.Value != null ).Select( n => n.Value ), newQuad, direction );
                updatedNeighbors.AddRange( newN );
            }
            foreach( var n in updatedNeighbors.Distinct() )
            {
                n.GenerateMeshData();
            }
        }

        /// <summary>
        /// Creates and caches the mesh for this LODQuad.
        /// </summary>
        private void GenerateMeshData()
        {
            var rebuild = new State.Rebuild();

            rebuild.Job = new MakeQuadMesh_Job();

            this.SetState( rebuild );
        }

        /// <summary>
        /// Destroy the gameobject representation of the quad, and removes that representation from the quadtree.
        /// </summary>
        private void Destroy()
        {
            Destroy( this.gameObject );
            this.Node.Value = null;
        }

        public static LODQuad CreateL0( Transform parent, LODQuadSphere quadSphere, CelestialBody celestialBody, LODQuadTree.Node node, float subdivisionDistance, Material mat, Direction3D face )
        {
            return Create( parent, face.ToVector3() * (float)celestialBody.Radius, quadSphere, celestialBody, Vector2.zero, 0, node, subdivisionDistance, mat, face );
        }

        static LODQuad Create( Transform parent, Vector3 localPosition, LODQuadSphere quadSphere, CelestialBody celestialBody, Vector2 center, int lN, LODQuadTree.Node node, float subdivisionDistance, Material mat, Direction3D face )
        {
            GameObject go = new GameObject( $"LODQuad L{lN}, {face}, ({center.x:#0.################}, {center.y:#0.################})" );
            go.transform.SetParent( parent );

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;

            go.AddComponent<MeshCollider>();

            LODQuad newQuad = go.AddComponent<LODQuad>();
            newQuad.Node = node;
            node.Value = newQuad;

            if( lN > 0 )
            {
                (int x, int y) = LODQuadTree_NodeUtils.GetChildIndex( newQuad.Node );

                newQuad.Node.Parent.Children[x, y].Value = newQuad;
            }

            newQuad.CelestialBody = celestialBody;
            newQuad._quadSphere = quadSphere;
            newQuad.SubdivisionDistance = subdivisionDistance;

            newQuad.SubdivisionLevel = lN;
            newQuad._quadSphereFace = face;

            // Unity keeps the local positions of objects internally.
            newQuad.transform.localPosition = localPosition;
            newQuad.transform.localRotation = Quaternion.identity;
            newQuad.transform.localScale = Vector3.one;
            newQuad.SetState( new State.Idle() );

            return newQuad;
        }
    }
}
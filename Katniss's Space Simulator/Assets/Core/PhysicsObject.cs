﻿using KatnisssSpaceSimulator.Core.Physics;
using KatnisssSpaceSimulator.Core.ReferenceFrames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KatnisssSpaceSimulator.Core
{
    /// <summary>
    /// Any object that calculates its own physics. This is a wrapper for some kind of internal physics solver / rigidbody.
    /// </summary>
    [RequireComponent( typeof( Rigidbody ) )]
    public class PhysicsObject : MonoBehaviour
    {
        // this is basically either a celestial body, or a vessel. Something that moves on its own and is not parented to anything else.

        /*ForceSolver forceSolver = new ForceSolver()
        {
            Mass = 5
        };*/

        Rigidbody rb;

        PhysicsScene test;

        /// <summary>
        /// Use this to add a force acting on the center of mass. Does not apply any torque.
        /// </summary>
        public void AddForce( Vector3 force )
        {
            this.rb.AddForce( force, ForceMode.Force );
        }

        /// <summary>
        /// Use this to add a force at a specified position instead of at the center of mass.
        /// </summary>
        public void AddForceAtPosition( Vector3 force, Vector3 position )
        {
            this.rb.AddForceAtPosition( force, position, ForceMode.Force );
        }

        public float Mass
        {
            get => this.rb.mass;
            set => this.rb.mass = value;
        }

        public Vector3 LocalCenterOfMass
        {
            get => this.rb.centerOfMass;
            set => this.rb.centerOfMass = value;
        }

        void Awake()
        {
            rb = this.GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 5;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Extrapolate;
        }

        void FixedUpdate()
        {
            if( Input.GetKey( KeyCode.W ) )
            {
                //AddForce( Vector3.up * 12.0f * rb.mass );
            }

            Vector3 gravityDir = Vector3.down;

            // F = m*a
            AddForce( gravityDir * 9.81f * rb.mass );



#warning TODO - this also needs to support floaring origin/krakensbane equivalent (resetting in-game velocity/position) to keep it running accurately.
            // frames of reference maybe can be used for that.

            // a rotating frame of reference will impart forces on the object just because it is rotating.
            // the reference frame needs to keep high precision position / rotation of the reference object.
            // - Every object's position will be transformed by this frame to get its "true" position, which might have arbitrary precision (and in reverse too, inverse to get local position).

            // There is only one global reference frame for the scene.

            // objects that are not centered on the reference frame need to be updated every frame (possibly less if they're very distant and can't be seen) to remain correct.
            // - if the frame is centered on the active vessel, then "world" space in Unity needs to be transformed into local space for that frame.
            // - - This can be done by applying forces/changing positions manually.


            // Bottom line is that we need to make the Unity's world space act like the local space of the selected reference frame.


            // ---------------------

            // There's also multi-scene physics, which apparently might be used to put the origin of the simulation at 2 different vessels, and have their positions accuratly updated???
            // doesn't seem like that to me reading the docs tho, but idk.


            //forceSolver.AddForce( (Vector3.down * 9.81f) / (float)forceSolver.Mass );

            // do more stuff here.

            // Advance and update transform values.
            //forceSolver.Advance( Time.fixedDeltaTime );

            //this.transform.position = referenceFrame.TransformPosition( forceSolver.Position );
            // rotation?
        }
    }
}

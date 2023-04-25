﻿using KatnisssSpaceSimulator.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KatnisssSpaceSimulator.Terrain
{
    /// <summary>
    /// Wraps around 6 faces of a sphere.
    /// </summary>
    [RequireComponent( typeof( CelestialBody ) )]
    public class LODQuadSphere : MonoBehaviour
    {
        public int DefaultSubdivisions { get; private set; } = 7;

        LODQuad[] _l0faces;
        CelestialBody _celestialBody;

        void Awake()
        {
            // Possibly move this to a child, so it can be disabled without disabling entire CB.
            _celestialBody = this.GetComponent<CelestialBody>();
        }

        void Start()
        {
            _l0faces = new LODQuad[6];
            for( int i = 0; i < 6; i++ )
            {
                _l0faces[i] = LODQuad.Create( _celestialBody.transform, _celestialBody.Radius, DefaultSubdivisions, Vector2.zero, 0, (QuadSphereFace)i );
                LODQuad.Subdivide( _l0faces[i] );
            }
        }
    }
}
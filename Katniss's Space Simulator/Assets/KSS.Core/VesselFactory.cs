﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KSS.Core
{
    /// <summary>
    /// A class responsible for instantiating a vessel from a source (save file, on launch, etc).
    /// </summary>
    public sealed class VesselFactory
    {
        // add source (save file / in memory scene change, etc).

        const string name = "tempname_vessel";

        /// <summary>
        /// Creates a new partless vessel at the specified global position.
        /// </summary>
        /// <param name="airfPosition">The `Absolute Inertial Reference Frame` position of the vessel to create.</param>
        /// <param name="airfRotation">Rotation of the vessel in the `Absolute Inertial Reference Frame`</param>
        /// <returns>The created partless vessel.</returns>
        public Vessel CreatePartless( Vector3Dbl airfPosition, Quaternion airfRotation, Vector3 sceneVelocity, Vector3 sceneAngularVelocity )
        {
            Vessel vessel = CreateGO( airfPosition, airfRotation );

            vessel.PhysicsObject.Velocity = sceneVelocity;
            vessel.PhysicsObject.AngularVelocity = sceneAngularVelocity;

            return vessel;
        }

        private static Vessel CreateGO( Vector3Dbl airfPosition, Quaternion airfRotation )
        {
            GameObject vesselGO = new GameObject( $"Vessel, '{name}'" );

            Vessel vessel = vesselGO.AddComponent<Vessel>();
            vessel.name = name;
            vessel.SetPosition( airfPosition );
            vessel.SetRotation( airfRotation );

            return vessel;
        }

        /// <summary>
        /// Completely deletes a vessel and cleans up after it.
        /// </summary>
        public static void Destroy( Vessel vessel )
        {
            UnityEngine.Object.Destroy( vessel.gameObject );
        }
    }
}
﻿using KatnisssSpaceSimulator.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KatnisssSpaceSimulator.Functionalities.ResourceFlowSystem
{

    /// <summary>
    /// A container for a <see cref="Substance"/>.
    /// </summary>
    public class FBulkContainer_Sphere : Functionality, IBulkContainer
    {
        /// <summary>
        /// Determines the center position of the container.
        /// </summary>
        [field: SerializeField]
        public Transform VolumeTransform { get; set; }

        /// <summary>
        /// The total available volume of the container, in [m^3].
        /// </summary>
        [field: SerializeField]
        public float MaxVolume { get; set; }

        [field: SerializeField]
        public float Radius { get; set; }

        [field: SerializeField]
        public SubstanceStateMultiple Contents { get; set; }

        /// <summary>
        /// Inflow minus outflow. positive = inflow, negative = outflow.
        /// </summary>
        [field: SerializeField]
        public SubstanceStateMultiple Inflow { get; set; }

        public SubstanceStateMultiple Sample( Vector3 localPosition, Vector3 localAcceleration, float holeArea )
        {
            if( this.Contents == null )
            {
                return SubstanceStateMultiple.NoFlow;
            }

            SubstanceState[] substances = this.Contents.GetSubstances();

            if( substances.Length == 0 )
            {
                return SubstanceStateMultiple.NoFlow;
            }

            float heightOfLiquid = SolveHeightOfTruncatedSphere( this.Contents.GetVolume() / this.MaxVolume ) * this.Radius;

            float distanceAlongAcceleration = Vector3.Dot( localPosition, localAcceleration.normalized );

            // Adjust the height of the fluid column based on the distance along the acceleration direction
            heightOfLiquid += distanceAlongAcceleration;
            heightOfLiquid -= this.Radius; // since both distance and height of truncated sphere already contain that.

            if( heightOfLiquid <= 0 )
            {
                return SubstanceStateMultiple.NoFlow;
            }

            // pressure in [Pa]
            float pressure = localAcceleration.magnitude * this.Contents.GetSubstances()[0].Data.Density * heightOfLiquid;

#warning TODO - assume not mixed?
            return new SubstanceStateMultiple( new FluidState() { Pressure = pressure, Temperature = 273.15f, Velocity = Vector3.zero }, Contents.GetSubstances() );
        }

        void FixedUpdate()
        {
            Contents.Add( Inflow, Time.fixedDeltaTime );
            //Volume += TotalInflow * Time.fixedDeltaTime;
            // TODO - update the mass too, because the fluid weighs something.
        }

        // flow between containers depends on many factors.
        // - acceleration on the container (ullage, only for liquids)
        // - pressure difference across the containers (both liquids and gasses).
        // - for mixed liquid + gas, gas will tend to accumulate on the opposite side to the liquid.
        // - for mixed liquid the densest will accumulate at the bottom.
        // - for mixed solid (gravel-like), orientation depends on where and when the resources came from. They don't tend to separate into layers based on density, unlike liquids and gasses.

        // - ullage purposes assume spherical tank??? maybe?
        //   - the orientation of the inlet could determine where on the sphere it is.

        // tanks could have an arbitrary number of connections depending on where the player puts the pipes?

        // the connections would be set up in the VAB / by the save loader.

        public override JToken Save()
        {
            throw new NotImplementedException();

            /*return new JObject()
            {
                { "Resources", this.Resources.ToString() }, // temp
                { "ConnectedTo", this.ConnectedTo.ToString() } // temp
            };*/
        }

        public override void Load( JToken data )
        {
            throw new NotImplementedException();

            //this.Resources = new Resource[] { }; // temp
            //this.ConnectedTo = new FBulkContainer[] { }; // temp
        }


        /// <summary>
        /// Calculates the height of a truncated unit sphere with the given volume as a [0..1] percentage of the unit sphere's volume.
        /// </summary>
        /// <returns>Value between 0 and 2.</returns>
        public static float SolveHeightOfTruncatedSphere( float volumePercent )
        {
            // https://math.stackexchange.com/questions/2364343/height-of-a-spherical-cap-from-volume-and-radius

            const float UnitSphereVolume = 4.18879020479f; // 4/3 * pi     -- radius=1
            const float TwoPi = 6.28318530718f;            // 2 * pi       -- radius=1
            const float Sqrt3 = 1.73205080757f;

            float Volume = UnitSphereVolume * volumePercent;

            float A = 1.0f - ((3.0f * Volume) / TwoPi); // A is a coefficient, [-1..1] for volumePercent in [0..1]
            float OneThirdArccosA = 0.333333333f * Mathf.Acos( A );
            float height = Sqrt3 * Mathf.Sin( OneThirdArccosA ) - Mathf.Cos( OneThirdArccosA ) + 1.0f;
            return height;
        }

    }
}
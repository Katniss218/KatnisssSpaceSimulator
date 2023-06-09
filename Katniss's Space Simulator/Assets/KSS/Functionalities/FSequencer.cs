﻿using KSS.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KSS.Control;
using UnityPlus.Serialization;
using System.Reflection;

namespace KSS.Functionalities
{
    [Obsolete( "It's a prototype" )]
    public class KeyboardSequenceElement : Sequence.Element
    {
        public KeyCode Key { get; set; } = KeyCode.Space;

        public override void Initialize()
        {
        }

        public override bool CanInvoke()
        {
            return Input.GetKey( Key );
        }
    }

    [Obsolete( "It's a prototype" )]
    public class TimedSequenceElement : Sequence.Element
    {
        /// <summary>
        /// The delay from the firing of the previous element, after which the sequence element should fire.
        /// </summary>
        public float Delay { get; set; }

        float _startTimestamp;
        float _timeSinceStart => _startTimestamp - Time.time;

        public override void Initialize()
        {
            _startTimestamp = Time.time;
        }

        public override bool CanInvoke()
        {
            return _timeSinceStart >= Delay;
        }
    }

    [Obsolete( "It's a prototype" )]
    public class Sequence
    {
        [Obsolete( "It's a prototype" )]
        public abstract class Element
        {
            public struct Action // sequencer uses its own parameters to call an arbitrary input action on the target.
            {
                public MethodInfo varargMethod;
                public object target;
                public object[] parameters;

                public void TryInvoke()
                {
                    try
                    {
                        varargMethod.Invoke( target, parameters );
                    }
                    catch( Exception ex )
                    {
                        Debug.LogException( ex );
                    }
                }
            }

            /// <summary>
            /// The actions that this sequence element will call when it's fired.
            /// </summary>
            public List<Action> Actions { get; private set; } = new List<Action>();

            /// <summary>
            /// Called when the previous action is triggerred, or on load (the first element).
            /// </summary>
            public abstract void Initialize();

            /// <summary>
            /// Called to check if the sequence element can fire in this frame.
            /// </summary>
            public abstract bool CanInvoke();

            /// <summary>
            /// Called to fire the sequence element.
            /// </summary>
            public void Invoke()
            {
                foreach( var action in Actions )
                {
                    action.TryInvoke();
                }
            }
        }

        public List<Element> Elements { get; private set; } = new List<Element>();

        public int Current { get; private set; } = 0;

        /// <summary>
        /// Tries to initialize the current element of the sequence.
        /// </summary>
        public bool TryInitialize()
        {
            if( Current < 0 || Current >= Elements.Count )
            {
                return false;
            }

            try
            {
                Elements[Current].Initialize();
                return true;
            }
            catch( Exception ex )
            {
                Debug.LogException( ex );
            }
            return false;
        }

        /// <summary>
        /// Tries to invoke the current element of the sequence.
        /// </summary>
        public bool TryInvoke()
        {
            if( Current < 0 || Current >= Elements.Count )
            {
                return false;
            }

            Element elem = Elements[Current];

            try
            {
                if( elem.CanInvoke() )
                {
                    elem.Invoke();
                    Current++;

                    TryInitialize(); // Initialize the next element.
                    return true;
                }
            }
            catch( Exception ex )
            {
                Debug.LogException( ex );
            }
            return false;
        }
    }

    /// <summary>
    /// Represents a controller that can invoke an arbitrary control action from a queue.
    /// </summary>
    [Obsolete( "It's a prototype" )]
    public class FSequencer : MonoBehaviour, IPersistent
    {
        public List<Sequence> Sequences { get; private set; } = new List<Sequence>();

        void Start()
        {
            foreach( var seq in Sequences )
            {
                seq.TryInitialize();
            }
        }

        void Update()
        {
            foreach( var seq in Sequences )
            {
                seq.TryInvoke();
            }
        }


        public void SetData( ILoader l, SerializedData data )
        {
            throw new NotImplementedException();
        }

        public SerializedData GetData( ISaver s )
        {
            throw new NotImplementedException();
        }
    }
}
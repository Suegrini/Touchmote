﻿using System;
using System.Collections.Generic;

namespace WiiTUIO.Provider
{
    /// <summary>
    /// The FrameEventArgs event defines the most basic type update that can be generated by an IProvider.
    /// A frame is designed to be a capsule for many contacts which a provider recieves within a given unit of time.
    /// </summary>
    /// <remarks>I find it helpful to think of them as a bit like a frame in a computer game where many items of game logic affect many updates before being reflected in the final render on screen.</remarks>
    public class FrameEventArgs : EventArgs
    {
        /// <summary>
        /// A list of contacts which were captured for this frame.
        /// </summary>
        public IEnumerable<WiiContact> Contacts { get; protected set; }

        /// <summary>
        /// A timestamp which describes when this frame was created.
        /// </summary>
        public ulong Timestamp { get; protected set; }

        /// <summary>
        /// Construct a new FrameEventArgs event.
        /// </summary>
        /// <param name="iTimestamp">The timestamp to create the event with.</param>
        /// <param name="lContacts">The list of contacts we are responsible for transmitting.</param>
        public FrameEventArgs(ulong iTimestamp, IEnumerable<WiiContact> lContacts)
        {
            // Ensure we have some contacts (otherwise the frame is kinda pointless!)
            if (lContacts == null)
                throw new Exception("FrameEventArgs cannot be created with no contacts.");

            // Store the variables.
            Contacts = lContacts;
            Timestamp = iTimestamp;
        }
    }
}

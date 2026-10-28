using System.Collections.Generic;
using UnityEngine;

/// STRATEGY 1 of 2 inside the Observer. Axis 1: Synchronous vs. Asynchronous.
/// The channel asks its policy two questions:
///   - Deliver:      an event was raised, hand it to these listeners
///   - OnSubscribed: a listener just arrived, should it hear anything that already happened?
/// A live channel answers "nothing" to the second one.
public abstract class DeliveryPolicy : ScriptableObject
{
    public abstract void Deliver(SocialChannel channel, SocialEvent e,
                                 List<ISocialListener> listeners);

    public virtual void OnSubscribed(SocialChannel channel, ISocialListener listener) { }
}

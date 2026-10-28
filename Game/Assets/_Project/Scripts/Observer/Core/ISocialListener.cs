/// The Observer contract. One method. Anything can implement it:
/// a UI, a bonus system, a counter, another member, a network sender.
public interface ISocialListener
{
    void OnSocialEvent(SocialEvent e);
}

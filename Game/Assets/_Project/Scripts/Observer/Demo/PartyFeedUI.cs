using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// OBSERVER example 3. A scrolling feed in a uGUI Text. Two ways to wire it:
///   - leave Viewer empty: it subscribes to the channel and shows everything (a global feed)
///   - set Viewer: it listens to that member's Heard event and shows only what that
///     member's role can see (a personal feed; this is how the Expert never sees the bomb)
/// Swap Text for TextMeshPro if your project uses it. Same idea either way.
public class PartyFeedUI : MonoBehaviour, ISocialListener
{
    public SocialChannel channel;
    public PartyMember viewer;              // optional
    public Text text;
    [Min(1)] public int maxLines = 8;

    private readonly List<string> lines = new List<string>();

    void OnEnable()
    {
        if (viewer != null) viewer.Heard += OnSocialEvent;    // variation 1
        else if (channel != null) channel.Subscribe(this);    // variation 2
        Redraw();
    }

    void OnDisable()
    {
        if (viewer != null) viewer.Heard -= OnSocialEvent;
        else if (channel != null) channel.Unsubscribe(this);
    }

    public void OnSocialEvent(SocialEvent e)
    {
        lines.Add(e.ToString());
        while (lines.Count > maxLines) lines.RemoveAt(0);
        Redraw();
    }

    void Redraw()
    {
        if (text == null) return;
        string header = viewer != null ? $"{viewer.displayName}'s feed" : "Party feed";
        text.text = header + "\n" + string.Join("\n", lines);
    }
}

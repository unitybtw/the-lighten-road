using System.Collections;
using NUnit.Framework;
using TheLightenRoad.Player;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class FollowCamera2DTests
{
    private GameObject cameraObject;
    private GameObject targetObject;
    private FollowCamera2D cameraFollow;
    private Rigidbody2D targetBody;

    [SetUp]
    public void SetUp()
    {
        targetObject = new GameObject("Test Target");
        targetObject.transform.position = Vector3.zero;
        targetBody = targetObject.AddComponent<Rigidbody2D>();
        targetBody.gravityScale = 0f;

        cameraObject = new GameObject("Test Camera");
        var cam = cameraObject.AddComponent<Camera>();
        cam.enabled = false;
        cameraFollow = cameraObject.AddComponent<FollowCamera2D>();
        cameraFollow.SetTarget(targetObject.transform, snap: true);
    }

    [TearDown]
    public void TearDown()
    {
        if (cameraObject != null)
            Object.Destroy(cameraObject);
        if (targetObject != null)
            Object.Destroy(targetObject);
    }

    [UnityTest]
    public IEnumerator FollowCameraSnapsAndTracksTargetWithOffset()
    {
        cameraFollow.Offset = new Vector3(0f, 2f, -10f);
        cameraFollow.SetTarget(targetObject.transform, snap: true);

        Assert.That(cameraObject.transform.position.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(cameraObject.transform.position.y, Is.EqualTo(2f).Within(0.01f));
        Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f).Within(0.01f));

        targetObject.transform.position = new Vector3(5f, 4f, 0f);
        yield return new WaitForSeconds(0.4f);

        Assert.That(cameraObject.transform.position.x, Is.GreaterThan(3f));
        Assert.That(cameraObject.transform.position.y, Is.GreaterThan(4f));
    }

    [UnityTest]
    public IEnumerator CameraLeadsAheadWhenTargetMovesHorizontally()
    {
        cameraFollow.EnableLookAhead = true;
        cameraFollow.LookAheadDistance = 1.0f;
        targetBody.linearVelocity = new Vector2(5f, 0f);

        yield return new WaitForSeconds(0.3f);
        Assert.That(cameraFollow.CurrentLookAheadX, Is.GreaterThan(0.5f),
            "Moving right should lead the camera on the positive X axis.");

        targetBody.linearVelocity = new Vector2(-5f, 0f);
        yield return new WaitForSeconds(0.4f);
        Assert.That(cameraFollow.CurrentLookAheadX, Is.LessThan(-0.5f),
            "Moving left should lead the camera on the negative X axis.");
    }

    [UnityTest]
    public IEnumerator CameraAnticipatesFallWhenDescendingRapidly()
    {
        cameraFollow.EnableFallAnticipation = true;
        cameraFollow.FallVelocityThreshold = -3f;
        cameraFollow.FallLeadDistance = 1.5f;

        targetBody.linearVelocity = new Vector2(0f, -8f);
        yield return new WaitForSeconds(0.3f);

        Assert.That(cameraFollow.CurrentFallLeadY, Is.LessThan(-0.5f),
            "Falling downward rapidly should shift the camera target downwards.");
    }

    [UnityTest]
    public IEnumerator CameraShakeActivatesAndDecays()
    {
        Assert.That(cameraFollow.IsShaking, Is.False);

        cameraFollow.Shake(0.2f, 0.15f);
        Assert.That(cameraFollow.IsShaking, Is.True);

        yield return new WaitForSeconds(0.05f);
        Assert.That(cameraFollow.IsShaking, Is.True);

        yield return new WaitForSeconds(0.15f);
        Assert.That(cameraFollow.IsShaking, Is.False, "Shake should expire after duration.");
    }
}

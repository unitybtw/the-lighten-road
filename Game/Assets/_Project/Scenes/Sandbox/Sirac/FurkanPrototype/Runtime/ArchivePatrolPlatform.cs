using System.Collections.Generic;
using TheLightenRoad.Player;
using UnityEngine;

namespace TheLightenRoad.Sirac.FurkanPrototype
{
    [DefaultExecutionOrder(100)] // Apply carrier movement after PlayerMotor2D has read jump/walk input.
    [RequireComponent(typeof(Rigidbody2D),typeof(BoxCollider2D))]
    public sealed class ArchivePatrolPlatform : MonoBehaviour
    {
        public Vector2[] Waypoints;
        public float Speed = 1.8f;
        public float CornerPause = 1f;
        [SerializeField] private bool powered;
        public bool Powered
        {
            get => powered;
            set
            {
                powered=value;
                if(!value && body!=null){body.linearVelocity=Vector2.zero;body.MovePosition(body.position);}
            }
        }
        private Rigidbody2D body;
        private BoxCollider2D surface;
        private int destination = 1;
        private float pause;
        private readonly HashSet<Rigidbody2D> riders = new HashSet<Rigidbody2D>();
        private readonly Collider2D[] contacts = new Collider2D[8];
        public float PauseRemaining => pause;
        public int Destination => destination;
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); surface = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            pause=CornerPause;
        }
        private void FixedUpdate() => TickMotion(Time.fixedDeltaTime);
        public void TickMotion(float dt)
        {
            if (!Powered || Waypoints == null || Waypoints.Length < 2 || dt <= 0) return;
            if (pause > 0) { body.linearVelocity=Vector2.zero;pause = Mathf.Max(0,pause-dt); return; }
            Vector2 next = Vector2.MoveTowards(body.position,Waypoints[destination],Speed*dt);
            Vector2 delta = next-body.position;
            // Find feet above the surface every physics step. Collision-stay callbacks can
            // disappear briefly when an ascending kinematic body lifts a dynamic rider.
            Bounds bounds=surface.bounds;
            int count=Physics2D.OverlapBox(new Vector2(bounds.center.x,bounds.max.y+.10f),
                new Vector2(bounds.size.x+.12f,.42f),0,new ContactFilter2D {useTriggers=false},contacts);
            for(int i=0;i<count;i++)
            {
                var motor=contacts[i].GetComponentInParent<PlayerMotor2D>();
                if(motor!=null && contacts[i].bounds.min.y>=bounds.max.y-.18f)riders.Add(motor.Body);
            }
            riders.RemoveWhere(rider => rider == null || !rider.simulated ||
                rider.linearVelocity.y > Mathf.Max(4f,body.linearVelocity.y+2f) ||
                rider.position.x < bounds.min.x-.15f || rider.position.x > bounds.max.x+.15f ||
                Mathf.Abs(rider.GetComponent<Collider2D>().bounds.min.y-bounds.max.y) > .45f);
            foreach (var rider in riders)
                // MovePosition combines the motor's relative walk with platform transport.
                // A real jump detaches above; gravity is not integrated twice while standing.
                rider.MovePosition(rider.position+delta+new Vector2(rider.linearVelocity.x*dt,0));
            body.MovePosition(next);
            if ((next-Waypoints[destination]).sqrMagnitude < .0001f)
            { destination=(destination+1)%Waypoints.Length;pause=CornerPause; }
        }
        public void ResetPlatform()
        {
            Powered = false; pause = CornerPause; destination = 1;riders.Clear();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (Waypoints != null && Waypoints.Length > 0) body.position=Waypoints[0];
            body.linearVelocity=Vector2.zero;
        }
    }
}

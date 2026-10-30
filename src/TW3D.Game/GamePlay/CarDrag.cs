using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Dynamics.Joints;
using TW3D.GameObjects;
using TW3D.Graphics;
using TW3D.Input;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The car grabbed with the mouse or a finger and dragged around: a joint pulls the body of the car after the pointer
    /// for as long as it is down.
    /// </summary>
    sealed class CarDrag
    {
        //The most force the drag pulls with, for each unit of the mass of the body of the car
        private const float MaxForcePerMass = 400f;

        private readonly World world;
        private readonly Car car;
        private readonly ChaseCamera camera;
        private FixedMouseJoint joint;
        private int pointerId;

        public CarDrag(World world, Car car, ChaseCamera camera)
        {
            this.world = world;
            this.car = car;
            this.camera = camera;
        }

        /// <summary>Follows the pointers; true when one of them grabs the car in this frame.</summary>
        /// <param name="controls">The on-screen controls; a pointer operating them does not grab the car.</param>
        public bool Update(InputState input, OnScreenControls controls, Viewport viewport)
        {
            bool grabbed = false;
            bool pointerFound = false;

            foreach (Pointer pointer in input.Pointers)
            {
                if (joint == null)
                {
                    if (pointer.Phase == PointerPhase.Pressed && !controls.Uses(pointer) && car.IsIntersected(pointer.Position.X, pointer.Position.Y))
                    {
                        joint = new FixedMouseJoint(car.car, car.car.Position) { MaxForce = MaxForcePerMass * car.car.Mass };
                        world.Add(joint);
                        car.car.Awake = true;
                        pointerId = pointer.Id;
                        pointerFound = true;
                        grabbed = true;
                    }
                }
                else if (pointer.Id == pointerId && pointer.IsDown)
                {
                    pointerFound = true;
                    Vector2? target = camera.ScreenToWorld(pointer.Position, viewport);
                    if (target.HasValue)
                        joint.WorldAnchorB = target.Value;
                }
            }

            //The pointer was released or lost
            if (joint != null && !pointerFound)
            {
                world.Remove(joint);
                joint = null;
            }
            return grabbed;
        }
    }
}

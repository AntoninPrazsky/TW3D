using System.Collections.Generic;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Dynamics.Contacts;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using TW3D.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics.Joints;
using System;

namespace TW3D.GameObjects
{
    /// <summary>A wheel slipping on what it stands on (see <see cref="Car.SlippingWheels"/>).</summary>
    /// <param name="Wheel">Which wheel of the physics (see <see cref="Car.WheelSide"/>).</param>
    /// <param name="Point">Where it touches the ground (physics coordinates).</param>
    /// <param name="Ground">What it stands on: the track or an object.</param>
    /// <param name="Speed">How fast the tyre slips over it (physics units a second).</param>
    readonly record struct WheelSlip(int Wheel, Vector2 Point, Body Ground, float Speed);

    class Car : GameObject
    {
        public Body car;
        
        private Body holderB;
        private Body wheel4LB;
        private Body wheel4RB;
        private Body wheel4TB;

        private Body holderF;
        private Body wheel4LF;
        private Body wheel4RF;
        private Body wheel4TF;
        
        private WheelJoint springB, springF;

        /// <summary>The car of the catalog this is.</summary>
        public CarType Type { get; }

        /// <summary>How many wheels the physics has (each is a pair of wheels, one on each side of the car).</summary>
        public const int WheelCount = 6;
        //The wheels, the rear ones first; how far the middle of the rear and the front wheels is from the middle of the
        //car across the track (scene units)
        private Body[] wheels;
        private float rearSide, frontSide;
        
        private RevoluteJoint springLeftB, springRightB, springTopB, springLeftF, springRightF, springTopF;
        List<RevoluteJoint> wJoints = new List<RevoluteJoint>();

        //The car drives towards -x of its model (its headlights point there)
        private static readonly Vector3 Forward = -Vector3.UnitX;

        private Model carModel;
        //The wheels on the far side of the car are the models turned around, those on the near side the models mirrored
        //front to back (built from the same file, see content/Content.mgcb), so that both turn their tread the same way
        private Model frontWheelModel, rearWheelModel;
        private Model nearFrontWheelModel, nearRearWheelModel;
        private Model holderModel;

        //How fast the motors turn the wheels at most (radians per second)
        private readonly float topSpeed;
        //The radius of the wheels (physics units) and the most torque of their motors
        private const float WheelRadius = 1.7f;
        private const float MotorTorque = 900f;
        //Traction: a wheel on the ground whose tyre spins faster over it than this (physics units a second) gets less
        //torque, none spinning faster by this much more, so that it grips instead of spinning; at a launch the tyres may
        //spin more for a while (screeching and smoking), the more the slower the car goes, no more from this speed. The
        //motors never hold the wheels back, and wheels in the air spin up freely.
        private const float GripSlip = 3f;
        private const float SlipFade = 4f;
        private const float LaunchSlip = 35f;
        private const float LaunchSpeed = 14f;
        //A burnout at a launch: the throttle pressed with the car going slower than this (physics units a second), the
        //motors spin the wheels with all their torque and the tyres grip only this share of their grip, so the wheels
        //really spin and the car creeps. It lasts while the tyres spin faster than this over the ground (physics units a
        //second) for this long (seconds of the physics), the last share of it gripping again; it is over when they have
        //not started spinning this long after the launch (the axles turning instead, or the car against an obstacle)
        private const float BurnoutSpeed = 3f;
        private const float BurnoutGrip = 0.08f;
        private const float BurnoutSpin = 15f;
        private const float BurnoutTime = 1.8f;
        private const float BurnoutFade = 0.4f;
        private const float BurnoutWait = 2f;
        //The spinning left of the burnout and how long since its launch (seconds of the physics); whether the motors
        //drove the wheels and how fast the fastest spinning tyre on the ground spun at the last update; when that was
        private float burnoutLeft, burnoutAge;
        private bool driving;
        private float mostSpinning;
        private double lastUpdate;
        //Braking ends a launch: the tyres do not spin up as at a launch (LaunchSlip) until the throttle is let go or the
        //car goes faster than LaunchSpeed
        private bool launchBraked;

        public float acceleration;

        public bool brake;

        //The brake works against the turning of each wheel in the world (as the old brake, which held the wheels still,
        //did), with a torque that builds up over this long after it is pressed (seconds of the physics) to this much at
        //most: more than any tyre grips, so a hard brake would lock the wheels. The ABS does not let it: a wheel on the
        //ground turning slower than it would roll by this share of its speed over the ground is about to lock, so its
        //brake lets go until the tyre nearly rolls again (slips less than this share), then grabs again with this share
        //of the torque it locked at, building up again by this share a second. The wheels so lock only for a moment, and
        //brake better than sliding: a tyre slipping more than the ABS lets it grips less, locked only this share of its
        //grip. Below this speed of the car (physics units a second) the ABS is off, and a wheel the brake has stopped
        //(turning slower than this, radians a second) is held still until the brake is let go: the car stands on a
        //slope, and climbs stairs on braked wheels turning the axles, as with the old brake
        private const float BrakeTorque = 6000f;
        private const float BrakeBuildTime = 0.25f;
        private const float AbsLockSlip = 0.2f;
        private const float AbsRollSlip = 0.1f;
        private const float AbsRegrab = 0.85f;
        private const float AbsBuildRate = 1.5f;
        private const float SlideGrip = 0.8f;
        private const float AbsSpeed = 6f;
        private const float HoldTurn = 0.5f;
        //How far the brake has built up (0 to 1); for each wheel (in the order of the wheels) its brake against the world,
        //the most of the built up brake the ABS lets it have, whether the ABS has let it go and whether it is held still
        private float brakePressure;
        private readonly FrictionJoint[] brakes = new FrictionJoint[WheelCount];
        private readonly float[] absShare = new float[WheelCount];
        private readonly bool[] absLetGo = new bool[WheelCount];
        private readonly bool[] held = new bool[WheelCount];

        public bool holderRotation;
        public bool holderRotationDir;

        public bool holderRotLockChnage;

        //The axles turn against the body. The player's motor turns them at this speed (radians a second) with at most
        //this torque, and when let go stops them with it for this long (seconds of the physics). Otherwise they hold
        //against the body with this much torque: the track turns them where it would lift a wheel off it (a hump, a
        //step, the start of a slope), but the motors of the wheels and small bumps do not, and they do not spin in the
        //air. Held, they also settle back on two wheels (as at the start, against the body) at this rate (1 / seconds)
        //and at most this fast (radians a second), so that the car does not stay standing on single wheels. Locked,
        //they keep their angle in the world instead, and the body turns freely around them
        private const float AxleTurnSpeed = 2.75f;
        private const float AxleTurnTorque = 20000f;
        private const float AxleStopTime = 0.15f;
        private const float AxleHoldTorque = 2500f;
        private const float AxleSettleRate = 3f;
        private const float AxleSettleSpeed = 1.5f;
        //How long the axles are still to be stopped by their motor (seconds of the physics)
        private float axleStopping;
        
        /// <summary>A car of the catalog with its wheels: one kind on the front axle, one on the rear one.</summary>
        public Car(World World, ScreenManager ScreenManager, ChaseCamera Camera, PhysicsClock Physics, Vector2 Position, CarType Type,
            WheelType FrontWheels, WheelType RearWheels)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            physics = Physics;
            position = Position;
            
            acceleration = 0f;

            carModel = screenManager.Content.Load<Model>(Type.Model);
            frontWheelModel = screenManager.Content.Load<Model>(FrontWheels.Model);
            rearWheelModel = screenManager.Content.Load<Model>(RearWheels.Model);
            nearFrontWheelModel = screenManager.Content.Load<Model>(FrontWheels.MirroredModel);
            nearRearWheelModel = screenManager.Content.Load<Model>(RearWheels.MirroredModel);
            holderModel = screenManager.Content.Load<Model>("holder");
            topSpeed = Type.TopSpeed;
            this.Type = Type;

            car = CreateModelBody(Type.Model, BodyType.Dynamic);
            car.Position = position;
            car.BodyType = BodyType.Dynamic;

            holderB = CreateModelBody("holder", BodyType.Dynamic);
            
            holderB.Position = position;
            
            holderB.BodyType = BodyType.Dynamic;

            float mSpeed = 0f;
            float mTorque = MotorTorque;

            CircleShape wheelShape = new CircleShape(WheelRadius, 0.8f);
            wheelShape.Density = 1;

            wheel4LB = world.CreateBody();
            wheel4LB.BodyType = BodyType.Dynamic;

            //Originally just position.X etc. instead of _holderB.Position.X (before the centre of the equilateral triangle was fixed)
            wheel4LB.Position = new Vector2(holderB.Position.X - 0.76454f * scale * 10, holderB.Position.Y + 0.644055f * scale * 10);
            wheel4LB.CreateFixture(wheelShape);
            wheel4LB.SetFriction(RearWheels.Friction);

            wheel4RB = world.CreateBody();
            wheel4RB.BodyType = BodyType.Dynamic;
            wheel4RB.Position = new Vector2(holderB.Position.X + 0.76454f * scale * 10, holderB.Position.Y + 0.644055f * scale * 10);
            wheel4RB.CreateFixture(wheelShape);
            wheel4RB.SetFriction(RearWheels.Friction);

            wheel4TB = world.CreateBody();
            wheel4TB.BodyType = BodyType.Dynamic;
            wheel4TB.Position = new Vector2(holderB.Position.X + 0, holderB.Position.Y - 0.680167f * scale * 10);
            wheel4TB.CreateFixture(wheelShape);
            wheel4TB.SetFriction(RearWheels.Friction);

            //Assembling the holder (axle)
            Vector2 axis = new Vector2(0, -1f);

            springLeftB = new RevoluteJoint(holderB, wheel4LB, wheel4LB.Position, true);
            springLeftB.MotorSpeed = mSpeed;

            springLeftB.MaxMotorTorque = mTorque;
            springLeftB.MotorEnabled = true;
            world.Add(springLeftB);

            springRightB = new RevoluteJoint(holderB, wheel4RB, wheel4RB.Position, true);
            springRightB.MotorSpeed = mSpeed;
            springRightB.MaxMotorTorque = mTorque;
            springRightB.MotorEnabled = true;
            world.Add(springRightB);

            springTopB = new RevoluteJoint(holderB, wheel4TB, wheel4TB.Position, true);
            springTopB.MotorSpeed = mSpeed;
            springTopB.MaxMotorTorque = mTorque;
            springTopB.MotorEnabled = true;
            world.Add(springTopB);

            //Rear axle offset
            float bx = -2.5f * scale * 12;
            float by = 0.8f * scale * 12;
            
            holderB.SetTransform(new Vector2(holderB.Position.X + bx, holderB.Position.Y + by), 0f);
            wheel4LB.SetTransform(new Vector2(wheel4LB.Position.X + bx, wheel4LB.Position.Y + by), 0f);
            wheel4RB.SetTransform(new Vector2(wheel4RB.Position.X + bx, wheel4RB.Position.Y + by), 0f);
            wheel4TB.SetTransform(new Vector2(wheel4TB.Position.X + bx, wheel4TB.Position.Y + by), 0f);

            float triangleFix = 0.288672f * scale * 10;
            
            springB = new WheelJoint(car, holderB, new Vector2(holderB.Position.X, holderB.Position.Y + triangleFix), axis, true);
            springB.MotorSpeed = 0f;
            springB.MaxMotorTorque = 10500f;
            springB.MotorEnabled = true;
            springB.Frequency = 4f;
            springB.DampingRatio = 0.7f;

            world.Add(springB);

            holderF = CreateModelBody("holder", BodyType.Dynamic);
            holderF.Position = position;
            holderF.BodyType = BodyType.Dynamic;

            wheel4LF = world.CreateBody();
            wheel4LF.BodyType = BodyType.Dynamic;
            wheel4LF.Position = new Vector2(position.X - 0.76454f * scale * 10, position.Y + 0.644055f * scale * 10);
            wheel4LF.CreateFixture(wheelShape);
            wheel4LF.SetFriction(FrontWheels.Friction);

            wheel4RF = world.CreateBody();
            wheel4RF.BodyType = BodyType.Dynamic;
            wheel4RF.Position = new Vector2(position.X + 0.76454f * scale * 10, position.Y + 0.644055f * scale * 10);
            wheel4RF.CreateFixture(wheelShape);
            wheel4RF.SetFriction(FrontWheels.Friction);

            wheel4TF = world.CreateBody();
            wheel4TF.BodyType = BodyType.Dynamic;
            wheel4TF.Position = new Vector2(position.X + 0, position.Y - 0.680167f * scale * 10);
            wheel4TF.CreateFixture(wheelShape);
            wheel4TF.SetFriction(FrontWheels.Friction);
            
            //Masses
            holderF.Mass = 15;
            holderB.Mass = 15;

            wheel4LF.Mass = FrontWheels.Mass;
            wheel4RF.Mass = FrontWheels.Mass;
            wheel4TF.Mass = FrontWheels.Mass;

            wheel4LB.Mass = RearWheels.Mass;
            wheel4RB.Mass = RearWheels.Mass;
            wheel4TB.Mass = RearWheels.Mass;

            car.Mass = Type.Mass;
            
            springLeftF = new RevoluteJoint(holderF, wheel4LF, wheel4LF.Position, true);
            springLeftF.MotorSpeed = mSpeed;
            springLeftF.MaxMotorTorque = mTorque;
            springLeftF.MotorEnabled = true;
            world.Add(springLeftF);

            springRightF = new RevoluteJoint(holderF, wheel4RF, wheel4RF.Position, true);
            springRightF.MotorSpeed = mSpeed;
            springRightF.MaxMotorTorque = mTorque;
            springRightF.MotorEnabled = true;
            world.Add(springRightF);

            springTopF = new RevoluteJoint(holderF, wheel4TF, wheel4TF.Position, true);
            springTopF.MotorSpeed = mSpeed;
            springTopF.MaxMotorTorque = mTorque;
            springTopF.MotorEnabled = true;
            world.Add(springTopF);

            //Front axle offset
            float fx = 3f * scale * 12;
            float fy = 0.8f * scale * 12;

            holderF.SetTransform(new Vector2(holderF.Position.X + fx, holderF.Position.Y + fy), 0f);
            
            wheel4LF.SetTransform(new Vector2(wheel4LF.Position.X + fx, wheel4LF.Position.Y + fy), 0f);
            wheel4RF.SetTransform(new Vector2(wheel4RF.Position.X + fx, wheel4RF.Position.Y + fy), 0f);
            wheel4TF.SetTransform(new Vector2(wheel4TF.Position.X + fx, wheel4TF.Position.Y + fy), 0f);
            
            springF = new WheelJoint(car, holderF, holderF.Position, axis, true);
            springF.MotorSpeed = 0f;
            springF.MaxMotorTorque = 10500f;
            springF.MotorEnabled = true;
            springF.Frequency = 4f;
            springF.DampingRatio = 0.7f;

            world.Add(springF);
            
            // debug
            wJoints.Add(springLeftB);
            wJoints.Add(springLeftF);
            wJoints.Add(springRightB);
            wJoints.Add(springRightF);
            wJoints.Add(springTopB);
            wJoints.Add(springTopF);
            
            car.SetCollisionCategories(CollisionCategories.CarBody);
            holderB.SetCollisionCategories(CollisionCategories.Holder);
            holderF.SetCollisionCategories(CollisionCategories.Holder);

            //The body and the wheels are each in their own band across the track (the wheels at the sides of the car),
            //so they touch different parts of some blocks (see ModelCollision)
            car.SetCollidesWith(Category.All & ~CollisionCategories.Holder & ~CollisionCategories.Wheel & ~CollisionCategories.TrackForWheels);
            holderB.SetCollidesWith(Category.None);
            holderF.SetCollidesWith(Category.None);
            foreach (Body wheel in new[] { wheel4LB, wheel4RB, wheel4TB, wheel4LF, wheel4RF, wheel4TF })
            {
                wheel.SetCollisionCategories(CollisionCategories.Wheel);
                wheel.SetCollidesWith(Category.All & ~CollisionCategories.TrackForBody);
            }

            //Every part knows its car, so a contact tells the car from the track and the objects (see ImpactSounds)
            foreach (Body body in new[] { car, holderB, holderF, wheel4LB, wheel4RB, wheel4TB, wheel4LF, wheel4RF, wheel4TF })
                body.Tag = this;

            wheels = new[] { wheel4LB, wheel4RB, wheel4TB, wheel4LF, wheel4RF, wheel4TF };
            rearSide = WheelsCentre(false, false).Z;
            frontSide = WheelsCentre(true, false).Z;

            //The brakes hold the wheels against the world (a body that does not move), only against turning
            Body still = world.CreateBody();
            for (int i = 0; i < wheels.Length; i++)
            {
                brakes[i] = new FrictionJoint(still, wheels[i], wheels[i].Position, true) { MaxForce = 0f, MaxTorque = 0f };
                world.Add(brakes[i]);
                absShare[i] = 1f;
            }
        }

        /// <summary>
        /// The wheels slipping on what they stand on (the track or an object) faster than a speed (physics units a second):
        /// braked wheels sliding, or wheels spinning at a launch. Where the tyres screech, leave skid marks and smoke.
        /// </summary>
        public void SlippingWheels(List<WheelSlip> slipping, float minSpeed)
        {
            slipping.Clear();
            for (int i = 0; i < wheels.Length; i++)
            {
                if (Ground(wheels[i], out Vector2 point, out Body ground, out float slip) && slip >= minSpeed)
                    slipping.Add(new WheelSlip(i, point, ground, slip));
            }
        }

        /// <summary>
        /// What a wheel stands on (the track or an object, not the car itself), where it touches it (physics coordinates)
        /// and how fast the tyre moves over it there (physics units a second): 0 rolling, the speed of the car sliding
        /// braked, more spinning.
        /// </summary>
        private bool Ground(Body wheel, out Vector2 point, out Body ground, out float slip)
        {
            for (ContactEdge edge = wheel.ContactList; edge != null; edge = edge.Next)
            {
                Contact contact = edge.Contact;
                if (!contact.IsTouching || contact.FixtureA.IsSensor || contact.FixtureB.IsSensor || edge.Other.Tag == this)
                    continue;
                contact.GetWorldManifold(out Vector2 normal, out FixedArray2<Vector2> points);
                point = points[0];
                ground = edge.Other;
                Vector2 relative = wheel.GetLinearVelocityFromWorldPoint(point) - ground.GetLinearVelocityFromWorldPoint(point);
                slip = Math.Abs(relative.Y * normal.X - relative.X * normal.Y);
                return true;
            }
            point = Vector2.Zero;
            ground = null;
            slip = 0f;
            return false;
        }

        /// <summary>
        /// Whether a wheel stands on something, and how much faster its tyre turns than it would rolling over it (physics
        /// units a second; less than 0 turning slower), and how fast the wheel moves over it.
        /// </summary>
        private bool Spinning(Body wheel, out float spinning, out float speed)
        {
            spinning = 0f;
            speed = 0f;
            if (!Ground(wheel, out Vector2 point, out Body ground, out _))
                return false;
            speed = (wheel.LinearVelocity - ground.GetLinearVelocityFromWorldPoint(point)).Length();
            spinning = Math.Abs(wheel.AngularVelocity - ground.AngularVelocity) * WheelRadius - speed;
            return true;
        }

        /// <summary>Lets the tyre of a wheel grip only a share of its grip on whatever it touches.</summary>
        private static void Grip(Body wheel, float share)
        {
            for (ContactEdge edge = wheel.ContactList; edge != null; edge = edge.Next)
            {
                edge.Contact.ResetFriction();
                if (share < 1f)
                    edge.Contact.Friction *= share;
            }
        }

        /// <summary>
        /// The ends of the exhaust pipes in the scene, one on each side of the car (see <see cref="CarType.Exhaust"/>), and
        /// which way they blow (to the back of the car); false for a car without an exhaust.
        /// </summary>
        public bool Exhausts(Vector3[] ends, out Vector3 blowing)
        {
            blowing = Vector3.Zero;
            if (Type.Exhaust is not { Length: 3 } exhaust)
                return false;
            Matrix body = ModelWorld(new Vector3(DrawPosition(car), 0f), DrawRotation(car), false) * Placement;
            ends[0] = Vector3.Transform(new Vector3(exhaust[0], exhaust[1], exhaust[2]), body);
            ends[1] = Vector3.Transform(new Vector3(exhaust[0], exhaust[1], -exhaust[2]), body);
            blowing = Vector3.Normalize(Vector3.TransformNormal(-Forward, body));
            return true;
        }

        /// <summary>The velocity of the car in the scene (scene units a second).</summary>
        public Vector3 Velocity => new Vector3(-car.LinearVelocity * Units.PhysicsToScene, 0f);

        /// <summary>How far a wheel (see <see cref="SlippingWheels"/>) is from the middle of the car across the track (scene units).</summary>
        public float WheelSide(int wheel) => wheel < 3 ? rearSide : frontSide;

        /// <summary>
        /// How fast the wheels turn on their axles, as a share of the top speed of the car: 0 standing, about 1 at full
        /// speed (more when they spin in the air or on ice).
        /// </summary>
        public float WheelSpin
        {
            get
            {
                //The wheels on the ground if any (they follow the car), else all of them (spinning freely in the air)
                float spin = 0f, grounded = 0f;
                int touching = 0;
                foreach (RevoluteJoint joint in wJoints)
                {
                    float turning = Math.Abs(joint.JointSpeed);
                    spin += turning;
                    if (Ground(joint.BodyB, out _, out _, out _))
                    {
                        grounded += turning;
                        touching++;
                    }
                }
                return (touching > 0 ? grounded / touching : spin / wJoints.Count) / topSpeed;
            }
        }

        /// <summary>
        /// How fast the spring of an axle (the rear or the front one) moves, in physics units a second: more than 0 while it
        /// compresses (the axle rising into the body), less than 0 while it rebounds.
        /// </summary>
        public float SpringSpeed(bool front)
        {
            WheelJoint spring = front ? springF : springB;
            //The axle's point of the joint against the point of the body where it is, along the axis of the spring (up the
            //body), which turns with the body
            Vector2 anchor = spring.WorldAnchorB;
            Vector2 axis = car.GetWorldVector(spring.LocalXAxis);
            return Vector2.Dot(spring.BodyB.GetLinearVelocityFromWorldPoint(anchor) - car.GetLinearVelocityFromWorldPoint(anchor), axis);
        }

        /// <summary>How fast the axles turn against the body (radians per second).</summary>
        public float AxleSpin => (Math.Abs(holderB.AngularVelocity - car.AngularVelocity) + Math.Abs(holderF.AngularVelocity - car.AngularVelocity)) / 2f;

        /// <summary>The speed of the car (physics units a second).</summary>
        public float Speed => car.LinearVelocity.Length();

        //The car is on its roof when its body is turned more than this from upright (radians, about 120°: not standing on
        //its nose or tail against a wall) and touches the track or an object; it lies still there while it moves and turns
        //slower than this (physics units a second, radians a second). It stands on its wheels while one of them touches the
        //ground and the body is turned less than this (radians, 90°)
        private const float RoofAngle = 2.1f;
        private const float StillSpeed = 1.5f;
        private const float StillTurn = 0.3f;
        private const float WheelsAngle = MathHelper.PiOver2;
        //Turned over by the development key, the car is lifted this far (physics units) before it falls onto its roof
        private const float TurnOverLift = 15f;

        /// <summary>How far the body is turned from upright, either way (radians, 0 to π).</summary>
        public float Tilt => Math.Abs(MathHelper.WrapAngle(car.Rotation));

        /// <summary>
        /// The car lies still on its roof: turned over, its body on the track or an object, neither moving nor turning (a
        /// car sliding down on its roof does not; one rocked by the axles as the player tries to right it the caller tells).
        /// </summary>
        public bool LiesStillOnRoof => Tilt > RoofAngle && Touches(car)
            && car.LinearVelocity.Length() < StillSpeed && Math.Abs(car.AngularVelocity) < StillTurn;

        /// <summary>The car stands on its wheels: one of them touches the ground and the body is not turned far over.</summary>
        public bool OnWheels
        {
            get
            {
                if (Tilt >= WheelsAngle)
                    return false;
                foreach (Body wheel in wheels)
                    if (Ground(wheel, out _, out _, out _))
                        return true;
                return false;
            }
        }

        /// <summary>A part of the car touches the track or an object (not the car itself).</summary>
        private bool Touches(Body part)
        {
            for (ContactEdge edge = part.ContactList; edge != null; edge = edge.Next)
            {
                Contact contact = edge.Contact;
                if (contact.IsTouching && !contact.FixtureA.IsSensor && !contact.FixtureB.IsSensor && edge.Other.Tag != this)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Turns the whole car upside down about the middle of its body and lifts it a little, still, so that it falls onto
        /// its roof (a development key, to try out a car on its roof).
        /// </summary>
        public void TurnOver()
        {
            Vector2 centre = car.Position;
            Vector2 lift = new Vector2(0f, -TurnOverLift);
            foreach (Body part in new[] { car, holderB, holderF, wheel4LB, wheel4RB, wheel4TB, wheel4LF, wheel4RF, wheel4TF })
            {
                //Half a turn about the middle: every part keeps its place on the car
                part.SetTransform(2f * centre - part.Position + lift, part.Rotation + MathHelper.Pi);
                part.LinearVelocity = Vector2.Zero;
                part.AngularVelocity = 0f;
            }
        }

        public override void Update()
        {
            if (car.Rotation >= 2 * Math.PI || car.Rotation <= -2 * Math.PI) { car.Rotation = 0; }

            float motorSpeed = Math.Sign(acceleration) * MathHelper.SmoothStep(0f, topSpeed, Math.Abs(acceleration));
            bool drive = Math.Abs(motorSpeed) >= topSpeed * 0.06f;

            //A launch starts a burnout, counted down while the tyres spin; letting go of the throttle or braking ends it
            double time = physics?.Time ?? 0.0;
            float step = (float)(time - lastUpdate);
            lastUpdate = time;
            if (!drive || brake)
                burnoutLeft = 0f;
            else if (!driving && Speed < BurnoutSpeed)
            {
                burnoutLeft = BurnoutTime;
                burnoutAge = 0f;
            }
            driving = drive;
            if (burnoutLeft > 0f)
            {
                burnoutAge += step;
                if (mostSpinning >= BurnoutSpin)
                    burnoutLeft -= step;
                else if (burnoutLeft >= BurnoutTime && burnoutAge > BurnoutWait)
                    burnoutLeft = 0f;
            }
            float burnout = MathHelper.Clamp(burnoutLeft / (BurnoutTime * BurnoutFade), 0f, 1f);
            if (brake)
                launchBraked = true;
            else if (!drive || Speed >= LaunchSpeed)
                launchBraked = false;

            float allowed = GripSlip + (launchBraked ? 0f : LaunchSlip * MathHelper.Clamp(1f - Speed / LaunchSpeed, 0f, 1f));
            bool abs = Speed >= AbsSpeed;
            bool standing = false;
            foreach (Body wheel in wheels)
                standing |= Ground(wheel, out _, out _, out _);
            mostSpinning = 0f;
            foreach (RevoluteJoint joint in wJoints)
            {
                //The brake cuts the drive of a wheel (the throttle fades out slowly after it is let go), unless it holds
                //the wheel still: the motor then turns the axle around it, as with the old brake
                joint.MotorSpeed = motorSpeed;
                joint.MotorEnabled = drive && (!brake || held[Array.IndexOf(wheels, joint.BodyB)]);
                float torque = 1f, grip = MathHelper.Lerp(1f, BurnoutGrip, burnout);
                if (Spinning(joint.BodyB, out float spinning, out float speed))
                {
                    mostSpinning = Math.Max(mostSpinning, spinning);
                    if (burnout <= 0f)
                        torque = MathHelper.Clamp(1f - (spinning - allowed) / SlipFade, 0f, 1f);
                    if (brake && abs && speed > 0f)
                        grip *= MathHelper.Lerp(1f, SlideGrip, MathHelper.Clamp((-spinning / speed - AbsLockSlip) / (1f - AbsLockSlip), 0f, 1f));
                }
                //A wheel lifted off the ground while the car stands on its others (the front one of a triangle under the
                //throttle) does not spin up faster either than it could on the ground at the speed of the car: it would
                //screech and smoke as it touched the ground again, as if the burnout went on
                else if (burnout <= 0f && standing)
                    torque = MathHelper.Clamp(1f - (Math.Abs(joint.BodyB.AngularVelocity) * WheelRadius - Speed - allowed) / SlipFade, 0f, 1f);
                joint.MaxMotorTorque = MotorTorque * torque;
                Grip(joint.BodyB, grip);
            }
            
            //The axles: turned by the player and stopped when let go, else held against the body, unless they are locked
            //(their motor would hold the body in the world then)
            axleStopping = holderRotation ? AxleStopTime : Math.Max(0f, axleStopping - step);
            bool turning = holderRotation || axleStopping > 0f && !holderRotLockChnage;
            foreach (WheelJoint axle in new[] { springB, springF })
            {
                if (holderRotation)
                    axle.MotorSpeed = holderRotationDir ? AxleTurnSpeed : -AxleTurnSpeed;
                else if (turning)
                    axle.MotorSpeed = 0f;
                else
                {
                    //How far the axle is turned from standing on two wheels (a third of a turn apart) against the body
                    float off = (float)Math.IEEERemainder(axle.BodyB.Rotation - axle.BodyA.Rotation, MathHelper.TwoPi / 3f);
                    axle.MotorSpeed = -MathHelper.Clamp(off * AxleSettleRate, -AxleSettleSpeed, AxleSettleSpeed);
                }
                axle.MaxMotorTorque = turning ? AxleTurnTorque : AxleHoldTorque;
                axle.MotorEnabled = holderRotation || !holderRotLockChnage;
            }

            Brake(step, abs);

            //The lock holds the axles
            SetFixedRotation(holderB, holderRotLockChnage);
            SetFixedRotation(holderF, holderRotLockChnage);
        }

        /// <summary>
        /// The brake (see <see cref="BrakeTorque"/>): builds up while it is pressed, and each wheel gets as much of it as
        /// its ABS lets it, or is held still once the car is slow and the wheel has stopped.
        /// </summary>
        /// <param name="step">Seconds of the physics since the last update.</param>
        /// <param name="abs">Whether the car goes fast enough for the ABS.</param>
        private void Brake(float step, bool abs)
        {
            brakePressure = brake ? Math.Min(1f, brakePressure + step / BrakeBuildTime) : 0f;
            for (int i = 0; i < wheels.Length; i++)
            {
                Body wheel = wheels[i];
                if (!brake)
                {
                    held[i] = false;
                    absLetGo[i] = false;
                    absShare[i] = 1f;
                }
                else if (!held[i])
                {
                    float share = Math.Min(brakePressure, absShare[i]);
                    if (!abs)
                    {
                        absLetGo[i] = false;
                        absShare[i] = 1f;
                        held[i] = Math.Abs(wheel.AngularVelocity) < HoldTurn;
                    }
                    //In the air the wheel keeps what the ABS did with it
                    else if (Spinning(wheel, out float spinning, out float speed) && speed > 0f)
                    {
                        float slip = -spinning / speed;
                        if (absLetGo[i])
                            absLetGo[i] = slip > AbsRollSlip;
                        else if (slip >= AbsLockSlip)
                        {
                            absLetGo[i] = true;
                            absShare[i] = share * AbsRegrab;
                        }
                        else
                            absShare[i] = Math.Min(1f, absShare[i] + AbsBuildRate * step);
                    }
                }
                //The brake is held where the wheel is, so the joint has no length (it only works against turning)
                brakes[i].LocalAnchorA = wheel.Position;
                brakes[i].MaxTorque = brake && !held[i] && !absLetGo[i] ? BrakeTorque * Math.Min(brakePressure, absShare[i]) : 0f;
                SetFixedRotation(wheel, held[i]);
            }
        }

        /// <summary>
        /// Keeps a body from turning, or lets it turn again. The physics works the mass of the body out from its shape
        /// then, so the mass it was given (the catalog's for the wheels) is put back.
        /// </summary>
        private static void SetFixedRotation(Body body, bool fixedRotation)
        {
            if (body.FixedRotation == fixedRotation)
                return;
            float mass = body.Mass;
            body.FixedRotation = fixedRotation;
            body.Mass = mass;
        }
        
        /// <summary>Height of the lowest point of the wheels in the 3D world (where the car stands), before <see cref="GameObject.Placement"/>.</summary>
        public float WheelsBottom
        {
            get
            {
                float bottom = float.MaxValue;
                foreach (Body wheel in new[] { wheel4LB, wheel4RB, wheel4TB, wheel4LF, wheel4RF, wheel4TF })
                {
                    //Physics y grows downwards; the 3D world is the physics world shrunk ten times and turned upside down
                    float radius = ((CircleShape)wheel.FixtureList[0].Shape).Radius;
                    bottom = Math.Min(bottom, -(wheel.Position.Y + radius) / 10f);
                }
                return bottom;
            }
        }

        /// <summary>
        /// Middle of the three wheels of an axle in the 3D world, before <see cref="GameObject.Placement"/>: on the side of
        /// the car towards -z (the wheels drawn as they are) or towards +z (the wheels drawn turned around).
        /// </summary>
        public Vector3 WheelsCentre(bool front, bool turned)
        {
            Body[] wheels = front ? new[] { wheel4LF, wheel4RF, wheel4TF } : new[] { wheel4LB, wheel4RB, wheel4TB };
            Vector2 middle = (DrawPosition(wheels[0]) + DrawPosition(wheels[1]) + DrawPosition(wheels[2])) / 3f;

            //The wheel models lie beside the car, away from their origin
            Model model = front ? frontWheelModel : rearWheelModel;
            Matrix[] transforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(transforms);
            float side = 0f;
            foreach (ModelMesh mesh in model.Meshes)
                side += Vector3.Transform(mesh.BoundingSphere.Center, transforms[mesh.ParentBone.Index]).Z / model.Meshes.Count;
            side *= scale;

            return new Vector3(-middle / 10f, turned ? -side : side);
        }

        public bool IsIntersected(float x, float y)
        {
            Ray pickRay = GetPickRay(x, y);

            float selectedDistance = float.MaxValue;

            Vector3 carModPos = new Vector3(-DrawPosition(car) / 10f, 0f);
            BoundingSphere sphere = new BoundingSphere(carModPos, 1f);
            
                Nullable<float> result = pickRay.Intersects(sphere);
                if (result.HasValue == true)
                {
                    if (result.Value < selectedDistance)
                    {
                        return true;
                    }
                }
                return false;
        }

        /// <summary>The colours of the car (its paint and the rims of its axles); null for the original ones.</summary>
        public CarColours Colours { get; set; }

        public override void Draw(SceneRenderer renderer)
        {
            Paint paint = Paint.Body(Colours), rear = Paint.Wheels(Colours, false), front = Paint.Wheels(Colours, true);
            DrawModel(renderer, carModel, new Vector3(DrawPosition(car), 0f), DrawRotation(car), false, paint);
            Matrix body = ModelWorld(new Vector3(DrawPosition(car), 0f), DrawRotation(car), false) * Placement;
            renderer.SetCar(carModel, body, Forward, brake);

            DrawModel(renderer, nearRearWheelModel, new Vector3(DrawPosition(wheel4LB), 0f), DrawRotation(wheel4LB), false, rear);
            DrawModel(renderer, nearRearWheelModel, new Vector3(DrawPosition(wheel4RB), 0f), DrawRotation(wheel4RB), false, rear);
            DrawModel(renderer, nearRearWheelModel, new Vector3(DrawPosition(wheel4TB), 0f), DrawRotation(wheel4TB), false, rear);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderB), 0f), DrawRotation(holderB), false);

            DrawModel(renderer, nearFrontWheelModel, new Vector3(DrawPosition(wheel4LF), 0f), DrawRotation(wheel4LF), false, front);
            DrawModel(renderer, nearFrontWheelModel, new Vector3(DrawPosition(wheel4RF), 0f), DrawRotation(wheel4RF), false, front);
            DrawModel(renderer, nearFrontWheelModel, new Vector3(DrawPosition(wheel4TF), 0f), DrawRotation(wheel4TF), false, front);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderF), 0f), DrawRotation(holderF), false);

            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4LB), 0f), DrawRotation(wheel4LB), true, rear);
            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4RB), 0f), DrawRotation(wheel4RB), true, rear);
            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4TB), 0f), DrawRotation(wheel4TB), true, rear);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderB), 0f), DrawRotation(holderB), true);

            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4LF), 0f), DrawRotation(wheel4LF), true, front);
            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4RF), 0f), DrawRotation(wheel4RF), true, front);
            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4TF), 0f), DrawRotation(wheel4TF), true, front);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderF), 0f), DrawRotation(holderF), true);
        }
    }
}

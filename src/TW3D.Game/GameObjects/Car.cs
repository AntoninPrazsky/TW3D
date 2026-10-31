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
        private Model frontWheelModel, rearWheelModel;
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

        public float acceleration;

        public bool brake;
        private bool braked;

        public bool holderRotation;
        public bool holderRotationDir;
        private bool holderRotated;

        public bool holderRotLockChnage;
        
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
        /// units a second; less than 0 turning slower).
        /// </summary>
        private bool Spinning(Body wheel, out float spinning)
        {
            spinning = 0f;
            if (!Ground(wheel, out Vector2 point, out Body ground, out _))
                return false;
            Vector2 over = wheel.LinearVelocity - ground.GetLinearVelocityFromWorldPoint(point);
            spinning = Math.Abs(wheel.AngularVelocity - ground.AngularVelocity) * WheelRadius - over.Length();
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

        /// <summary>How fast the axles turn against the body (radians per second).</summary>
        public float AxleSpin => (Math.Abs(holderB.AngularVelocity - car.AngularVelocity) + Math.Abs(holderF.AngularVelocity - car.AngularVelocity)) / 2f;

        /// <summary>The speed of the car (physics units a second).</summary>
        public float Speed => car.LinearVelocity.Length();

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

            float allowed = GripSlip + LaunchSlip * MathHelper.Clamp(1f - Speed / LaunchSpeed, 0f, 1f);
            mostSpinning = 0f;
            foreach (RevoluteJoint joint in wJoints)
            {
                joint.MotorSpeed = motorSpeed;
                joint.MotorEnabled = drive;
                float torque = 1f;
                if (Spinning(joint.BodyB, out float spinning))
                {
                    mostSpinning = Math.Max(mostSpinning, spinning);
                    if (burnout <= 0f)
                        torque = MathHelper.Clamp(1f - (spinning - allowed) / SlipFade, 0f, 1f);
                }
                joint.MaxMotorTorque = MotorTorque * torque;
                Grip(joint.BodyB, MathHelper.Lerp(1f, BurnoutGrip, burnout));
            }
            
            if (holderRotation)
            {
                if (!holderRotated) 
                {
                    springB.MotorEnabled = true;
                    springF.MotorEnabled = true;
                    if (holderRotationDir) 
                    { 
                        springB.MotorSpeed = 5f;
                        springF.MotorSpeed = 5f;
                    }
                    else
                    {
                        springB.MotorSpeed = -5f;
                        springF.MotorSpeed = -5f;
                    }
                    holderRotated = true;
                }
            }
            else
            {
                if (holderRotated)
                {
                    springB.MotorEnabled = false;
                    springF.MotorEnabled = false;
                    springB.MotorSpeed = 0f;
                    springF.MotorSpeed = 0f;
                    holderRotated = false;
                }
            }

            if (brake)
            {
                if (!braked)
                {
                    foreach (RevoluteJoint joint in wJoints)
                    {
                        joint.BodyB.FixedRotation = true;
                    }
                    braked = true;
                }
            }
            else
            {
                if (braked)
                {
                    foreach (RevoluteJoint joint in wJoints)
                    {
                        joint.BodyB.FixedRotation = false;
                        braked = false;
                    }
                }
            }
            holderB.FixedRotation = holderRotLockChnage;
            holderF.FixedRotation = holderRotLockChnage;
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

        public override void Draw(SceneRenderer renderer)
        {
            DrawModel(renderer, carModel, new Vector3(DrawPosition(car), 0f), DrawRotation(car), false);
            Matrix body = ModelWorld(new Vector3(DrawPosition(car), 0f), DrawRotation(car), false) * Placement;
            renderer.SetCar(carModel, body, Forward, brake);

            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4LB), 0f), DrawRotation(wheel4LB), false);
            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4RB), 0f), DrawRotation(wheel4RB), false);
            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4TB), 0f), DrawRotation(wheel4TB), false);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderB), 0f), DrawRotation(holderB), false);

            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4LF), 0f), DrawRotation(wheel4LF), false);
            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4RF), 0f), DrawRotation(wheel4RF), false);
            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4TF), 0f), DrawRotation(wheel4TF), false);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderF), 0f), DrawRotation(holderF), false);

            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4LB), 0f), DrawRotation(wheel4LB), true);
            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4RB), 0f), DrawRotation(wheel4RB), true);
            DrawModel(renderer, rearWheelModel, new Vector3(DrawPosition(wheel4TB), 0f), DrawRotation(wheel4TB), true);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderB), 0f), DrawRotation(holderB), true);

            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4LF), 0f), DrawRotation(wheel4LF), true);
            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4RF), 0f), DrawRotation(wheel4RF), true);
            DrawModel(renderer, frontWheelModel, new Vector3(DrawPosition(wheel4TF), 0f), DrawRotation(wheel4TF), true);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderF), 0f), DrawRotation(holderF), true);
        }
    }
}

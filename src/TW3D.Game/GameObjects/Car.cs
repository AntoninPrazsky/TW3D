using System.Collections.Generic;
using nkast.Aether.Physics2D.Collision.Shapes;
using nkast.Aether.Physics2D.Common;
using nkast.Aether.Physics2D.Dynamics;
using TW3D.GamePlay;
using TW3D.Graphics;
using TW3D.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using nkast.Aether.Physics2D.Dynamics.Joints;
using System;

namespace TW3D.GameObjects
{
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
        
        private RevoluteJoint springLeftB, springRightB, springTopB, springLeftF, springRightF, springTopF;
        List<RevoluteJoint> wJoints = new List<RevoluteJoint>();

        //The car drives towards -x of its model (its headlights point there)
        private static readonly Vector3 Forward = -Vector3.UnitX;

        private Model carModel;
        private Model wheelModel;
        private Model holderModel;

        public float acceleration;

        public bool brake;
        private bool braked;

        public bool holderRotation;
        public bool holderRotationDir;
        private bool holderRotated;

        public bool holderRotLockChnage;
        
        public Car(World World, ScreenManager ScreenManager, ChaseCamera Camera, PhysicsClock Physics, Vector2 Position, string CarName, string WheelName)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            physics = Physics;
            position = Position;
            
            acceleration = 0f;

            carModel = screenManager.Content.Load<Model>(CarName);
            wheelModel = screenManager.Content.Load<Model>(WheelName);
            holderModel = screenManager.Content.Load<Model>("holder");

            car = CreateModelBody(CarName, BodyType.Dynamic);
            car.Position = position;
            car.BodyType = BodyType.Dynamic;

            holderB = CreateModelBody("holder", BodyType.Dynamic);
            
            holderB.Position = position;
            
            holderB.BodyType = BodyType.Dynamic;

            float wFriction = 1f;
            float mSpeed = 0f;
            float mTorque = 900f;

            CircleShape wheelShape = new CircleShape(1.7f, 0.8f);
            wheelShape.Density = 1;

            wheel4LB = world.CreateBody();
            wheel4LB.BodyType = BodyType.Dynamic;

            //Originally just position.X etc. instead of _holderB.Position.X (before the centre of the equilateral triangle was fixed)
            wheel4LB.Position = new Vector2(holderB.Position.X - 0.76454f * scale * 10, holderB.Position.Y + 0.644055f * scale * 10);
            wheel4LB.CreateFixture(wheelShape);
            wheel4LB.SetFriction(wFriction);

            wheel4RB = world.CreateBody();
            wheel4RB.BodyType = BodyType.Dynamic;
            wheel4RB.Position = new Vector2(holderB.Position.X + 0.76454f * scale * 10, holderB.Position.Y + 0.644055f * scale * 10);
            wheel4RB.CreateFixture(wheelShape);
            wheel4RB.SetFriction(wFriction);

            wheel4TB = world.CreateBody();
            wheel4TB.BodyType = BodyType.Dynamic;
            wheel4TB.Position = new Vector2(holderB.Position.X + 0, holderB.Position.Y - 0.680167f * scale * 10);
            wheel4TB.CreateFixture(wheelShape);
            wheel4TB.SetFriction(wFriction);

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
            wheel4LF.SetFriction(wFriction);

            wheel4RF = world.CreateBody();
            wheel4RF.BodyType = BodyType.Dynamic;
            wheel4RF.Position = new Vector2(position.X + 0.76454f * scale * 10, position.Y + 0.644055f * scale * 10);
            wheel4RF.CreateFixture(wheelShape);
            wheel4RF.SetFriction(wFriction);

            wheel4TF = world.CreateBody();
            wheel4TF.BodyType = BodyType.Dynamic;
            wheel4TF.Position = new Vector2(position.X + 0, position.Y - 0.680167f * scale * 10);
            wheel4TF.CreateFixture(wheelShape);
            wheel4TF.SetFriction(wFriction);
            
            //Masses
            holderF.Mass = 15;
            holderB.Mass = 15;

            wheel4LF.Mass = 30;
            wheel4RF.Mass = 30;
            wheel4TF.Mass = 30;

            wheel4LB.Mass = 30;
            wheel4RB.Mass = 30;
            wheel4TB.Mass = 30;

            car.Mass = 200f;
            
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
        }

        public override void Update()
        {
            if (car.Rotation >= 2 * Math.PI || car.Rotation <= -2 * Math.PI) { car.Rotation = 0; }

            foreach (RevoluteJoint joint in wJoints)
            {
                joint.MotorSpeed = Math.Sign(acceleration) * MathHelper.SmoothStep(0f, 50f, Math.Abs(acceleration));
                
                if (Math.Abs(joint.MotorSpeed) < 50f * 0.06f)
                {
                    joint.MotorEnabled = false;
                }
                else
                {
                    joint.MotorEnabled = true;
                }
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
            Matrix body = ModelWorld(new Vector3(DrawPosition(car), 0f), DrawRotation(car), false);
            renderer.SetCar(carModel, body, Forward, brake);

            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4LB), 0f), DrawRotation(wheel4LB), false);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4RB), 0f), DrawRotation(wheel4RB), false);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4TB), 0f), DrawRotation(wheel4TB), false);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderB), 0f), DrawRotation(holderB), false);

            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4LF), 0f), DrawRotation(wheel4LF), false);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4RF), 0f), DrawRotation(wheel4RF), false);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4TF), 0f), DrawRotation(wheel4TF), false);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderF), 0f), DrawRotation(holderF), false);

            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4LB), 0f), DrawRotation(wheel4LB), true);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4RB), 0f), DrawRotation(wheel4RB), true);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4TB), 0f), DrawRotation(wheel4TB), true);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderB), 0f), DrawRotation(holderB), true);

            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4LF), 0f), DrawRotation(wheel4LF), true);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4RF), 0f), DrawRotation(wheel4RF), true);
            DrawModel(renderer, wheelModel, new Vector3(DrawPosition(wheel4TF), 0f), DrawRotation(wheel4TF), true);
            DrawModel(renderer, holderModel, new Vector3(DrawPosition(holderF), 0f), DrawRotation(holderF), true);
        }
    }
}

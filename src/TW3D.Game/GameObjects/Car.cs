using System.Collections.Generic;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using TW3D.DrawingSystem;
using TW3D.ScreenSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysics.Dynamics.Joints;
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
        
        public Car(World World, ScreenManager ScreenManager, Camera2D Camera, Vector2 Position, string CarName, string WheelName)
        {
            world = World;
            screenManager = ScreenManager;
            camera = Camera;
            position = Position;
            
            acceleration = 0f;

            carModel = screenManager.Content.Load<Model>(CarName);
            wheelModel = screenManager.Content.Load<Model>(WheelName);
            holderModel = screenManager.Content.Load<Model>("holder");

            car = CreatePolyBody("Bitmaps/" + CarName, 2f, BodyType.Dynamic);
            car.Position = position;
            car.BodyType = BodyType.Dynamic;

            holderB = CreatePolyBody("Bitmaps/holder", 2f, BodyType.Dynamic);
            
            holderB.Position = position;
            
            holderB.BodyType = BodyType.Dynamic;

            float wFriction = 1f;
            float mSpeed = 0f;
            float mTorque = 900f;

            CircleShape wheelShape = new CircleShape(1.7f, 0.8f);
            wheelShape.Density = 1;

            wheel4LB = new Body(world);
            wheel4LB.BodyType = BodyType.Dynamic;

            //Původně místo _holderB.Position.X jenom position.X apod. (před opravou středu rovnostranného trojúhelníku)
            wheel4LB.Position = new Vector2(holderB.Position.X - 0.76454f * scale * 10, holderB.Position.Y + 0.644055f * scale * 10);
            wheel4LB.CreateFixture(wheelShape);
            wheel4LB.Friction = wFriction;

            wheel4RB = new Body(world);
            wheel4RB.BodyType = BodyType.Dynamic;
            wheel4RB.Position = new Vector2(holderB.Position.X + 0.76454f * scale * 10, holderB.Position.Y + 0.644055f * scale * 10);
            wheel4RB.CreateFixture(wheelShape);
            wheel4RB.Friction = wFriction;

            wheel4TB = new Body(world);
            wheel4TB.BodyType = BodyType.Dynamic;
            wheel4TB.Position = new Vector2(holderB.Position.X + 0, holderB.Position.Y - 0.680167f * scale * 10);
            wheel4TB.CreateFixture(wheelShape);
            wheel4TB.Friction = wFriction;

            //Sestavení držáku (nápravy)
            Vector2 axis = new Vector2(0, -1f);

            springLeftB = new RevoluteJoint(holderB, wheel4LB, wheel4LB.Position, true);
            springLeftB.MotorSpeed = mSpeed;

            springLeftB.MaxMotorTorque = mTorque;
            springLeftB.MotorEnabled = true;
            world.AddJoint(springLeftB);

            springRightB = new RevoluteJoint(holderB, wheel4RB, wheel4RB.Position, true);
            springRightB.MotorSpeed = mSpeed;
            springRightB.MaxMotorTorque = mTorque;
            springRightB.MotorEnabled = true;
            world.AddJoint(springRightB);

            springTopB = new RevoluteJoint(holderB, wheel4TB, wheel4TB.Position, true);
            springTopB.MotorSpeed = mSpeed;
            springTopB.MaxMotorTorque = mTorque;
            springTopB.MotorEnabled = true;
            world.AddJoint(springTopB);

            //Posun zadní nápravy
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

            world.AddJoint(springB);

            holderF = CreatePolyBody("Bitmaps/holder", 2f, BodyType.Dynamic);
            holderF.Position = position;
            holderF.BodyType = BodyType.Dynamic;

            wheel4LF = new Body(world);
            wheel4LF.BodyType = BodyType.Dynamic;
            wheel4LF.Position = new Vector2(position.X - 0.76454f * scale * 10, position.Y + 0.644055f * scale * 10);
            wheel4LF.CreateFixture(wheelShape);
            wheel4LF.Friction = wFriction;

            wheel4RF = new Body(world);
            wheel4RF.BodyType = BodyType.Dynamic;
            wheel4RF.Position = new Vector2(position.X + 0.76454f * scale * 10, position.Y + 0.644055f * scale * 10);
            wheel4RF.CreateFixture(wheelShape);
            wheel4RF.Friction = wFriction;

            wheel4TF = new Body(world);
            wheel4TF.BodyType = BodyType.Dynamic;
            wheel4TF.Position = new Vector2(position.X + 0, position.Y - 0.680167f * scale * 10);
            wheel4TF.CreateFixture(wheelShape);
            wheel4TF.Friction = wFriction;
            
            //Hmotnosti
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
            world.AddJoint(springLeftF);

            springRightF = new RevoluteJoint(holderF, wheel4RF, wheel4RF.Position, true);
            springRightF.MotorSpeed = mSpeed;
            springRightF.MaxMotorTorque = mTorque;
            springRightF.MotorEnabled = true;
            world.AddJoint(springRightF);

            springTopF = new RevoluteJoint(holderF, wheel4TF, wheel4TF.Position, true);
            springTopF.MotorSpeed = mSpeed;
            springTopF.MaxMotorTorque = mTorque;
            springTopF.MotorEnabled = true;
            world.AddJoint(springTopF);

            //Posun přední nápravy
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

            world.AddJoint(springF);
            
            // debug
            wJoints.Add(springLeftB);
            wJoints.Add(springLeftF);
            wJoints.Add(springRightB);
            wJoints.Add(springRightF);
            wJoints.Add(springTopB);
            wJoints.Add(springTopF);
            
            car.CollisionCategories = Category.Cat1;
            holderB.CollisionCategories = Category.Cat2;
            holderF.CollisionCategories = Category.Cat2;
            wheel4LB.CollisionCategories = Category.Cat3;
            wheel4RB.CollisionCategories = Category.Cat3;
            wheel4TB.CollisionCategories = Category.Cat3;
            wheel4LF.CollisionCategories = Category.Cat3;
            wheel4RF.CollisionCategories = Category.Cat3;
            wheel4TF.CollisionCategories = Category.Cat3;

            car.CollidesWith = Category.All & ~Category.Cat2 & ~Category.Cat3;
            holderB.CollidesWith = Category.None;
            holderF.CollidesWith = Category.None;

            camera.TrackingBody = car;
            camera.EnableTracking = true;
            camera.EnableRotationTracking = true;
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

            Vector3 carModPos = new Vector3(-car.Position.X/10, -car.Position.Y/10, 0f);
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

        public override void Draw()
        {
            DrawModel(carModel, new Vector3(car.Position.X, car.Position.Y, 0f), car.Rotation, false, true);

            DrawModel(wheelModel, new Vector3(wheel4LB.Position.X, wheel4LB.Position.Y, 0f), wheel4LB.Rotation, false, true);
            DrawModel(wheelModel, new Vector3(wheel4RB.Position.X, wheel4RB.Position.Y, 0f), wheel4RB.Rotation, false, true);
            DrawModel(wheelModel, new Vector3(wheel4TB.Position.X, wheel4TB.Position.Y, 0f), wheel4TB.Rotation, false, true);
            DrawModel(holderModel, new Vector3(holderB.Position.X, holderB.Position.Y, 0f), holderB.Rotation, false, true);

            DrawModel(wheelModel, new Vector3(wheel4LF.Position.X, wheel4LF.Position.Y, 0f), wheel4LF.Rotation, false, true);
            DrawModel(wheelModel, new Vector3(wheel4RF.Position.X, wheel4RF.Position.Y, 0f), wheel4RF.Rotation, false, true);
            DrawModel(wheelModel, new Vector3(wheel4TF.Position.X, wheel4TF.Position.Y, 0f), wheel4TF.Rotation, false, true);
            DrawModel(holderModel, new Vector3(holderF.Position.X, holderF.Position.Y, 0f), holderF.Rotation, false, true);

            DrawModel(wheelModel, new Vector3(wheel4LB.Position.X, wheel4LB.Position.Y, 0f), wheel4LB.Rotation, true, true);
            DrawModel(wheelModel, new Vector3(wheel4RB.Position.X, wheel4RB.Position.Y, 0f), wheel4RB.Rotation, true, true);
            DrawModel(wheelModel, new Vector3(wheel4TB.Position.X, wheel4TB.Position.Y, 0f), wheel4TB.Rotation, true, true);
            DrawModel(holderModel, new Vector3(holderB.Position.X, holderB.Position.Y, 0f), holderB.Rotation, true, true);

            DrawModel(wheelModel, new Vector3(wheel4LF.Position.X, wheel4LF.Position.Y, 0f), wheel4LF.Rotation, true, true);
            DrawModel(wheelModel, new Vector3(wheel4RF.Position.X, wheel4RF.Position.Y, 0f), wheel4RF.Rotation, true, true);
            DrawModel(wheelModel, new Vector3(wheel4TF.Position.X, wheel4TF.Position.Y, 0f), wheel4TF.Rotation, true, true);
            DrawModel(holderModel, new Vector3(holderF.Position.X, holderF.Position.Y, 0f), holderF.Rotation, true, true);
        }
    }
}

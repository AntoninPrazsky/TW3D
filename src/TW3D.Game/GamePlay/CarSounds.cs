using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using TW3D.Audio;
using TW3D.GameObjects;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The sounds of the car while it is driven: the engine (two loops in step, idling and under the throttle, pitched by
    /// how fast the wheels turn, so spinning wheels rev it), the motor of the axles while they turn, the lock of the axles,
    /// the brake grabbing the wheels of a moving car, the tyres screeching while they slip (sliding braked or spinning)
    /// and the shock absorbers of each axle creaking at each fast stroke of its spring (see <see cref="SpringCreak"/>).
    /// The loops pause with the game.
    /// </summary>
    sealed class CarSounds : IDisposable
    {
        //The pitch of the engine (octaves) with the wheels standing and turning at the top speed of the car, before the
        //car's own (CarType.EnginePitch: a sportier car revs higher, and follows the wheels faster, EngineResponse); how
        //fast the throttle and the motor of the axles fade in and out (1 / seconds)
        private const float IdlePitch = -0.4f;
        private const float TopPitch = 0.6f;
        private const float FadeRate = 10f;
        //Under the throttle the idling loop drops to this share of its volume, the other loop taking over
        private const float IdleUnderLoad = 0.5f;
        //The pitch of the motor of the axles turning slowly and at this speed against the body (radians per second)
        private const float AxlesLowPitch = -0.3f;
        private const float AxlesHighPitch = 0.3f;
        private const float AxlesTopSpin = 8f;
        //The brake sounds when it grabs the wheels of a car going at least this fast, at its loudest from this speed
        //(physics units a second)
        private const float BrakeMinSpeed = 8f;
        private const float BrakeFullSpeed = 40f;
        //The tyres screech while they slip over the ground (see Car.SlippingWheels), at their loudest and highest from
        //this speed of slipping (physics units a second)
        private const float SkidFullSpeed = 40f;
        //The axles unlocked sound higher and softer than locked
        private const float UnlockPitch = 0.4f;
        private const float UnlockVolume = 0.5f;

        private readonly GameSounds sounds;
        private readonly Car car;
        //Null without an audio device
        private readonly SoundEffectInstance idle, load, axles, skid;
        private readonly SpringCreak rearSpring = new SpringCreak(), frontSpring = new SpringCreak();
        //How long no axle may creak, while one creak plays (seconds): two at once sang like a voice
        private float creakQuiet;
        private float pitch = float.NaN;
        private float throttle;
        private float axlesLevel;
        private float skidLevel;
        private bool braked, locked;

        public CarSounds(GameSounds sounds, Car car)
        {
            this.sounds = sounds;
            this.car = car;
            idle = sounds.Loop(Sound.EngineIdle);
            load = sounds.Loop(Sound.EngineLoad);
            axles = sounds.Loop(Sound.AxleMotor);
            skid = sounds.Loop(Sound.Skid);
        }

        /// <param name="elapsed">Real seconds since the last update.</param>
        /// <param name="running">The game runs; when it is paused, the loops pause.</param>
        /// <param name="throttleHeld">How far the throttle is held, 0..1.</param>
        /// <param name="slipping">The wheels slipping on the ground (null while the game does not run).</param>
        public void Update(float elapsed, bool running, float throttleHeld, bool brake, bool lockAxles, bool axlesTurning, List<WheelSlip> slipping)
        {
            if (!running)
            {
                foreach (SoundEffectInstance loop in Loops)
                    if (loop?.State == SoundState.Playing)
                        loop.Pause();
                return;
            }

            //The engine: higher as the wheels turn faster (rising quickly at first, slower towards the top), the loop under
            //the throttle fading in as it is held
            if (float.IsNaN(pitch))
                pitch = IdlePitch + car.Type.EnginePitch;
            float follow = 1f - MathF.Exp(-car.Type.EngineResponse * elapsed);
            float fade = 1f - MathF.Exp(-FadeRate * elapsed);
            float revs = MathF.Sqrt(MathHelper.Clamp(car.WheelSpin, 0f, 1f));
            pitch = MathHelper.Lerp(pitch, MathHelper.Lerp(IdlePitch, TopPitch, revs) + car.Type.EnginePitch, follow);
            throttle = MathHelper.Lerp(throttle, MathHelper.Clamp(throttleHeld, 0f, 1f), fade);
            Set(idle, Sound.EngineIdle, 1f - (1f - IdleUnderLoad) * throttle, pitch);
            Set(load, Sound.EngineLoad, throttle, pitch);

            axlesLevel = MathHelper.Lerp(axlesLevel, axlesTurning ? 1f : 0f, fade);
            Set(axles, Sound.AxleMotor, axlesLevel, MathHelper.Lerp(AxlesLowPitch, AxlesHighPitch, MathHelper.Clamp(car.AxleSpin / AxlesTopSpin, 0f, 1f)));

            float slip = 0f;
            foreach (WheelSlip wheel in slipping)
                slip = Math.Max(slip, wheel.Speed);
            float skidShare = MathHelper.Clamp(slip / SkidFullSpeed, 0f, 1f);
            skidLevel = MathHelper.Lerp(skidLevel, slip > 0f ? MathHelper.Lerp(0.3f, 1f, skidShare) : 0f, fade);
            Set(skid, Sound.Skid, skidLevel, MathHelper.Lerp(-0.2f, 0.2f, skidShare));

            creakQuiet -= elapsed;
            Creak(rearSpring, car.SpringSpeed(false), elapsed);
            Creak(frontSpring, car.SpringSpeed(true), elapsed);

            if (lockAxles != locked)
            {
                if (lockAxles)
                    sounds.Play(Sound.AxleLock);
                else
                    sounds.Play(Sound.AxleLock, UnlockPitch, UnlockVolume);
                locked = lockAxles;
            }

            if (brake && !braked && car.Speed >= BrakeMinSpeed)
                sounds.Play(Sound.Brake, 0f, MathHelper.Lerp(0.4f, 1f, MathHelper.Clamp((car.Speed - BrakeMinSpeed) / (BrakeFullSpeed - BrakeMinSpeed), 0f, 1f)));
            braked = brake;
        }

        /// <summary>Follows the spring of an axle and plays its creak, unless another creak still plays.</summary>
        private void Creak(SpringCreak spring, float speed, float elapsed)
        {
            if (spring.Update(speed, elapsed, out float volume) && creakQuiet <= 0f)
            {
                sounds.Play(Sound.Suspension, 0f, volume);
                creakQuiet = SpringCreak.QuietTime;
            }
        }

        private SoundEffectInstance[] Loops => new[] { idle, load, axles, skid };

        /// <summary>Sets a loop's volume (a share of the sound's own) and pitch, and starts or resumes it.</summary>
        private static void Set(SoundEffectInstance loop, Sound sound, float level, float loopPitch)
        {
            if (loop == null)
                return;
            loop.Volume = MathHelper.Clamp(SoundBakes.Volume(sound) * level, 0f, 1f);
            loop.Pitch = MathHelper.Clamp(loopPitch, -1f, 1f);
            if (loop.State == SoundState.Paused)
                loop.Resume();
            else if (loop.State == SoundState.Stopped)
                loop.Play();
        }

        public void Dispose()
        {
            foreach (SoundEffectInstance loop in Loops)
            {
                loop?.Stop();
                loop?.Dispose();
            }
        }
    }
}

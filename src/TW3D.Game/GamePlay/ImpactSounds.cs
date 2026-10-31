using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using nkast.Aether.Physics2D.Dynamics;
using nkast.Aether.Physics2D.Dynamics.Contacts;
using TW3D.Audio;
using TW3D.GameObjects;
using TW3D.Shared;

namespace TW3D.GamePlay
{
    /// <summary>
    /// The sounds of things hitting each other. For every contact the physics solves (Aether's post-solve), the impulse
    /// tells how much the contact changed the speed of each body; the hardest hit of a frame for each sound (the car, a
    /// metal object, a wooden one) is heard, as loud as it was, and that body not again until a moment later. A body
    /// resting on another is pushed a little every step, far below what is heard. The bodies are told apart by their tag:
    /// the parts of the car carry the car, the dynamic objects their type (with its material); the track carries nothing.
    /// </summary>
    sealed class ImpactSounds : IDisposable
    {
        //A hit changing the speed of a body by this much is heard, and at its loudest from this much (physics units a
        //second). The car hits harder: its light wheels take every bump of the road (measured: driving on a flat road
        //changes their speed by about 3 a step, a landing by 100 to 250); a barrel pushed by the car keeps touching it.
        //With these, the car is heard about once a second over humps and once a step up stairs.
        private const float CarMinChange = 30f;
        private const float CarFullChange = 150f;
        private const float ObjectMinChange = 12f;
        private const float ObjectFullChange = 60f;
        //The softest hit heard, as a share of the sound's volume
        private const float SoftestVolume = 0.15f;
        //A body is not heard again until this long after its hit (seconds): one hit lasts a few steps of the physics
        private const float QuietTime = 0.15f;
        //An object this heavy (kg) plays as baked; twice as heavy, half an octave lower; within these pitches (octaves)
        private const float BakedMass = 60f;
        private const float LowestPitch = -0.8f;
        private const float HighestPitch = 0.5f;

        private readonly World world;
        private readonly GameSounds sounds;
        //The hardest hit of each body in this frame (the parts of the car as one body), and how long each must stay quiet
        private readonly Dictionary<object, (float Change, float Mass, Sound Sound)> hits = new Dictionary<object, (float, float, Sound)>();
        private readonly Dictionary<object, float> quiet = new Dictionary<object, float>();
        private readonly List<object> keys = new List<object>();

        public ImpactSounds(World world, GameSounds sounds)
        {
            this.world = world;
            this.sounds = sounds;
            world.ContactManager.PostSolve += OnPostSolve;
        }

        private void OnPostSolve(Contact contact, ContactVelocityConstraint impulse)
        {
            Body a = contact.FixtureA.Body, b = contact.FixtureB.Body;
            //The parts of the car touching each other are no hit
            if (a.Tag is Car && a.Tag == b.Tag)
                return;
            float total = 0f;
            for (int i = 0; i < impulse.pointCount; i++)
                total += impulse.points[i].normalImpulse;
            Note(a, total);
            Note(b, total);
        }

        private void Note(Body body, float impulse)
        {
            if (body.BodyType != BodyType.Dynamic || body.Mass <= 0f)
                return;
            Sound sound;
            object key;
            switch (body.Tag)
            {
                case Car car:
                    sound = Sound.ImpactCar;
                    key = car;
                    break;
                case ObjectType type:
                    sound = type.Material == ObjectMaterial.Wood ? Sound.ImpactWood : Sound.ImpactMetal;
                    key = body;
                    break;
                default:
                    return;
            }
            float change = impulse / body.Mass;
            if (!hits.TryGetValue(key, out var hit) || change > hit.Change)
                hits[key] = (change, body.Mass, sound);
        }

        /// <summary>Sounds the hardest hits of the frame; called after the physics has advanced.</summary>
        /// <param name="elapsed">Real seconds since the last update.</param>
        public void Update(float elapsed)
        {
            keys.Clear();
            keys.AddRange(quiet.Keys);
            foreach (object key in keys)
            {
                float left = quiet[key] - elapsed;
                if (left > 0f)
                    quiet[key] = left;
                else
                    quiet.Remove(key);
            }

            foreach (Sound sound in new[] { Sound.ImpactCar, Sound.ImpactMetal, Sound.ImpactWood })
            {
                float min = sound == Sound.ImpactCar ? CarMinChange : ObjectMinChange;
                float full = sound == Sound.ImpactCar ? CarFullChange : ObjectFullChange;
                object hardest = null;
                float change = min, mass = 0f;
                foreach (KeyValuePair<object, (float Change, float Mass, Sound Sound)> hit in hits)
                    if (hit.Value.Sound == sound && hit.Value.Change >= change && !quiet.ContainsKey(hit.Key))
                    {
                        hardest = hit.Key;
                        change = hit.Value.Change;
                        mass = hit.Value.Mass;
                    }
                if (hardest == null)
                    continue;

                float volume = MathHelper.Lerp(SoftestVolume, 1f, MathHelper.Clamp((change - min) / (full - min), 0f, 1f));
                float pitch = sound == Sound.ImpactCar ? 0f : MathHelper.Clamp(-0.5f * MathF.Log2(mass / BakedMass), LowestPitch, HighestPitch);
                sounds.Play(sound, pitch, volume);
                quiet[hardest] = QuietTime;
            }
            hits.Clear();
        }

        public void Dispose()
        {
            world.ContactManager.PostSolve -= OnPostSolve;
        }
    }
}

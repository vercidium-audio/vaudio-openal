using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace vaudio_openal;

internal class Scene
{
    OpenALSystem openal;
    OpenALSource alSource;

    vaudio.RaytracingContext context;
    vaudio.Emitter listener;
    vaudio.Emitter speech;
    vaudio.PrismPrimitive clothPrism;
    List<vaudio.PrismPrimitive> concretePrisms = [];

    Stopwatch watch = Stopwatch.StartNew();

    internal Scene()
    {
        InitialiseVAudio();
        InitialiseOpenAL();
    }

    void InitialiseVAudio()
    {
        // Create a Vercidium Audio context
        context = new()
        {
            //RenderingEnabled = true,
            WorldSize = new(100),
            OnReverbUpdated = OnReverbUpdated
        };


        // Create a listener that casts occlusion and permeation rays
        listener = new()
        {
            Name = "Listener",
            PermeationRayCount = 32,
            PermeationBounceCount = 3,
            ReverbRayCount = 128,
            ReverbBounceCount = 24,
            MaxEchogramTime = 5000,
            EchogramGranularity = 50,
            Position = new vaudio.Vector3F(40, 50, 50),

            // Customise ray rendering
            //PermeationColor = new vaudio.Color(255, 150, 0, 150),
            //TrailColor = new vaudio.Color(255, 255, 255, 50),
        };

        // Set the energy cap based on reverb ray counts
        listener.ReverbEnergyCap = listener.ReverbRayCount * listener.ReverbBounceCount * 0.05f;

        context.AddEmitter(listener);


        // Create a target emitter that will be discovered by the listener
        speech = new()
        {
            Name = "Speech",
            Position = new vaudio.Vector3F(50),
            OnRaytracedByAnotherEmitter = OnSpeechRaytraced
        };

        context.AddEmitter(speech);
        listener.AddTarget(speech);


        // Add a cloth prism to the simulation
        clothPrism = new()
        {
            size = new(4),
            material = vaudio.MaterialType.Cloth
        };

        context.AddPrimitive(clothPrism);

        // Left and right
        {
            var prism = new vaudio.PrismPrimitive()
            {
                material = vaudio.MaterialType.Concrete,
                size = new(100, 100, 1),
                transform = vaudio.Matrix4F.CreateTranslation(50, 50, 0),
            };

            concretePrisms.Add(prism);
            context.AddPrimitive(prism);
        }

        {
            var prism = new vaudio.PrismPrimitive()
            {
                material = vaudio.MaterialType.Concrete,
                size = new(100, 100, 1),
                transform = vaudio.Matrix4F.CreateTranslation(50, 50, 100),
            };

            concretePrisms.Add(prism);
            context.AddPrimitive(prism);
        }

        // Front and back
        {
            var prism = new vaudio.PrismPrimitive()
            {
                material = vaudio.MaterialType.Concrete,
                size = new(1, 100, 100),
                transform = vaudio.Matrix4F.CreateTranslation(0, 50, 50),
            };

            concretePrisms.Add(prism);
            context.AddPrimitive(prism);
        }

        {
            var prism = new vaudio.PrismPrimitive()
            {
                material = vaudio.MaterialType.Concrete,
                size = new(1, 100, 100),
                transform = vaudio.Matrix4F.CreateTranslation(100, 50, 50),
            };

            concretePrisms.Add(prism);
            context.AddPrimitive(prism);
        }

        // Top and bottom
        {
            var prism = new vaudio.PrismPrimitive()
            {
                material = vaudio.MaterialType.Concrete,
                size = new(100, 1, 100),
                transform = vaudio.Matrix4F.CreateTranslation(50, 0, 50),
            };

            concretePrisms.Add(prism);
            context.AddPrimitive(prism);
        }

        {
            var prism = new vaudio.PrismPrimitive()
            {
                material = vaudio.MaterialType.Concrete,
                size = new(100, 1, 100),
                transform = vaudio.Matrix4F.CreateTranslation(50, 100, 50),
            };

            concretePrisms.Add(prism);
            context.AddPrimitive(prism);
        }

        // Reduce the transmission of the cloth material, so we can hear the sound when the prism moves on top of the speech Emitter 
        var cloth = context.GetMaterial(vaudio.MaterialType.Cloth);
        cloth.TransmissionLF = 2.5f;
        cloth.TransmissionHF = 5.0f;

        context.MaterialsDirty = true;
    }

    void OnReverbUpdated()
    {
        openal.UpdateReverb(listener.EAX);
    }

    void InitialiseOpenAL()
    {
        openal = new OpenALSystem();
        openal.LoadSoundData("resource/audio/speech.ogg");

        // Face the listener towards the speech Emitter
        openal.SetListenerPosition(listener.Position.GetPosition(), 0, -MathF.PI / 2);
    }

    // This callback is invoked when the listener raytraces the speech Emitter
    void OnSpeechRaytraced(vaudio.Emitter other)
    {
        // Create a sound and low-pass filter
        alSource = openal.CreateSound(speech.Position.GetPosition());

        // Update the filter
        UpdateLowPassFilter();

        // Play the sound
        alSource.Play();
    }

    internal void Update()
    {
        // Move the prism onto the speech Emitter to muffle it
        {
            var lerp = (MathF.Sin(watch.ElapsedMilliseconds / 1500.0f + 1.25f) + 1) / 2;
            clothPrism.transform = vaudio.Matrix4F.CreateTranslation(Lerp(50.0f, 55.0f, lerp), 50, 50);
        }

        // Enclose/open the area
        {
            var lerp = (MathF.Sin(watch.ElapsedMilliseconds / 900.0f + 1.25f) + 1) / 2;

            if (lerp < 0.5f)
                lerp = 0;
            else
                lerp = 1;

            var radius = Lerp(100, 25, lerp);

            var size = new vaudio.Vector3F(radius, radius, 1);
            concretePrisms[0].size = size;
            concretePrisms[1].size = size;

            size = new vaudio.Vector3F(1, radius, radius);
            concretePrisms[2].size = size;
            concretePrisms[3].size = size;

            size = new vaudio.Vector3F(radius, 1, radius);
            concretePrisms[4].size = size;
            concretePrisms[5].size = size;
        }

        // Update the raytracing context
        context.Update();

        // Update the low pass filter
        if (listener.HasRaytracedTarget(speech))
        {
            UpdateLowPassFilter();
        }

        // Update OpenAL
        openal.Update();
    }

    void UpdateLowPassFilter()
    {
        var filter = listener.GetTargetFilter(speech);

        alSource.UpdateFilter(filter);
    }

    // Helper method
    static float Lerp(float current, float target, float lerp) => current + (target - current) * lerp;
}

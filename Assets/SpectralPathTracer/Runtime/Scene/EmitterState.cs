using System;
using UnityEngine;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Every value an emitter's light depends on, so the compiler can tell when Timeline changed one.
     * </summary>
     */
    public readonly struct EmitterState : IEquatable<EmitterState>
    {
        #region Properties

        public EmissionKind Kind { get; }
        public Color Color { get; }
        public MeasuredSpectrum Spectrum { get; }
        public float Temperature { get; } // kelvin
        public float Strength { get; } // luminance Y

        #endregion


        public EmitterState(EmissionKind kind, Color color, MeasuredSpectrum spectrum, float temperature, float strength)
        {
            Kind = kind;
            Color = color;
            Spectrum = spectrum;
            Temperature = temperature;
            Strength = strength;
        }

        public bool Equals(EmitterState other)
        {
            // Color.Equals is exact, where == would miss a tiny animated step
            return Kind == other.Kind
                && Color.Equals(other.Color)
                && Spectrum == other.Spectrum
                && Temperature == other.Temperature
                && Strength == other.Strength;
        }

        public override bool Equals(object obj)
        {
            return obj is EmitterState other && Equals(other);
        }

        public override int GetHashCode()
        {
            // a hash may skip fields; equal states still match
            return HashCode.Combine(Kind, Temperature, Strength);
        }
    }
}

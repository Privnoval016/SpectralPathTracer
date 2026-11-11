using UnityEngine;

namespace SpectralPathTracer
{
    // fits a sigmoid polynomial spectrum to an sRGB color under D65 based on Jakob and Hanika 2019
    public static class SigmoidFit
    {
        #region Constants

        private const int MaxIterations = 15; // if it takes more than this we know its cooked
        private const float Converged = 1e-12f; // squared CIELAB error below which the fit stops

        private const float InitialDamping = 1e-3f; // starts close to gauss newton
        private const int MaxDampingAttempts = 10; // after this many failed attempts we give up


        private const float WavelengthRange = Cie.LastWavelength - Cie.FirstWavelength; // 470 nm so it maps the fit onto [0, 1]

        private const float LabDelta = 6f / 29f; // cube root is basically linear below this

        #endregion

        #region Private Fields

        // row i is D65 times the matching functions at 360 + i nm, scaled so D65 itself has Y = 1
        private static readonly Vector3[] Weights = BuildWeights();

        // D65 at Y = 1, the white CIELAB measures colors against
        private static readonly Vector3 White = Sum(Weights);

        #endregion

        /**
         * <summary>
         * Improves sigmoid coefficients until their spectrum lit by D65 matches a color.
         * </summary>
         * <param name="rgb">The linear sRGB color to match.</param>
         * <param name="coefficients">The starting guess, replaced by the fit, for wavelengths mapped onto [0, 1].</param>
         * <returns>The remaining CIELAB distance, the CIE76 delta E.</returns>
         */
        public static float Fit(Vector3 rgb, ref Vector3 coefficients)
        {
            // damped least squared damped least squared


            Vector3 target = Lab(Cie.LinearSrgbToXyz(rgb), out _);
            float cost = Cost(coefficients, target, out Vector3 residual, out Matrix4x4 jacobian); // cost = |residual|^2, jacobian = d residual / d coefficients
            float damping = InitialDamping;

            for (int iteration = 0; iteration < MaxIterations && cost > Converged; iteration++)
            {
                // linearize the residual, r(c + step) ~ r + J step, and set up its normal equations
                Matrix4x4 jacobianT = jacobian.transpose;
                Matrix4x4 normal = jacobianT * jacobian; // J^T J
                Vector3 gradient = jacobianT.MultiplyVector(residual); // J^T r

                // damp the normal equations to make a downhill step and increase the damping if it overshoots
                bool accepted = false;
                for (int attempt = 0; attempt < MaxDampingAttempts && !accepted; attempt++)
                {
                    // (J^T J + damping I) step = J^T r, where a large damping makes a short downhill step
                    Matrix4x4 damped = normal;
                    damped.m00 += damping;
                    damped.m11 += damping;
                    damped.m22 += damping;

                    Vector3 step = damped.inverse.MultiplyVector(gradient); // a singular matrix inverts to zero which then fails below
                    Vector3 trial = coefficients - step;
                    float trialCost = Cost(trial, target, out Vector3 trialResidual, out Matrix4x4 trialJacobian);

                    if (trialCost < cost)
                    {
                        coefficients = trial;
                        cost = trialCost;
                        residual = trialResidual;
                        jacobian = trialJacobian;

                        damping = Mathf.Max(damping * 0.5f, 1e-12f); // the step worked, so trust the linear model more
                        accepted = true;
                    }
                    else
                        damping *= 10f; // the step overshot so take a shorter one
                }

                // no damping found a better point womp womp
                if (!accepted)
                    break;
            }


            return Mathf.Sqrt(cost);
        }

        /**
         * <summary>
         * Converts coefficients fitted on wavelengths mapped onto [0, 1] into coefficients for wavelengths in nm.
         * </summary>
         * <param name="normalized">c0, c1, and c2 for (lambda - 360) / 470.</param>
         * <returns>c0, c1 and c2 for lambda in nm.</returns>
         */
        public static Vector3 ToNanometres(Vector3 normalized)
        {
            const float offset = Cie.FirstWavelength;
            const float scale = 1f / WavelengthRange;

            // expand c0 (scale (lambda - offset))^2 + c1 scale (lambda - offset) + c2 and collect powers of lambda
            float a = normalized.x * scale * scale;
            float b = normalized.y * scale - 2f * normalized.x * offset * scale * scale;
            float c = normalized.z - normalized.y * offset * scale + normalized.x * (offset * scale) * (offset * scale);


            return new Vector3(a, b, c);
        }

        /**
         * <summary>
         * Converts a linear sRGB color to CIELAB relative to D65.
         * </summary>
         * <param name="rgb">The linear sRGB color.</param>
         * <returns>The color in CIELAB.</returns>
         */
        public static Vector3 SrgbToLab(Vector3 rgb)
        {
            return Lab(Cie.LinearSrgbToXyz(rgb), out _);
        }

        #region Helpers

        /**
         * <summary>
         * Measures how far a spectrum's color is from the target and its jacobian.
         * </summary>
         * <param name="coefficients">The coefficients, for wavelengths mapped onto [0, 1].</param>
         * <param name="target">The target color in CIELAB.</param>
         * <param name="residual">The spectrum's CIELAB minus the target's.</param>
         * <param name="jacobian">d residual / d coefficients, rows L a b and columns c0 c1 c2.</param>
         * <returns>The squared CIELAB distance.</returns>
         */
        private static float Cost(Vector3 coefficients, Vector3 target, out Vector3 residual, out Matrix4x4 jacobian)
        {
            Vector3 xyz = Vector3.zero;
            Vector3 byC0 = Vector3.zero, byC1 = Vector3.zero, byC2 = Vector3.zero; // columns of d xyz / d coefficients

            // this is basically a matrix multiply of the 3x470 weights by the 470x3 derivatives
            for (int row = 0; row < Cie.SampleCount; row++)
            {
                float lambda = row / WavelengthRange; // row i is 360 + i nm, mapped onto [0, 1]
                float x = (coefficients.x * lambda + coefficients.y) * lambda + coefficients.z;

                // the spectrum adds to xyz in proportion to its value HERE
                xyz += SigmoidSpectrum.Sigmoid(x) * Weights[row];

                // and ds / dc = S'(x) (lambda^2, lambda, 1) by chain rule
                Vector3 slope = SigmoidSlope(x) * Weights[row];
                byC0 += slope * (lambda * lambda);
                byC1 += slope * lambda;
                byC2 += slope;
            }

            Matrix4x4 xyzByCoefficients = Matrix4x4.identity; // only the upper 3x3 is used so the 1 in m33 keeps the blocks separate
            xyzByCoefficients.SetColumn(0, byC0);
            xyzByCoefficients.SetColumn(1, byC1);
            xyzByCoefficients.SetColumn(2, byC2);


            // more chain rule: d lab / d c = (d lab / d xyz) (d xyz / d c)
            residual = Lab(xyz, out Matrix4x4 labByXyz) - target;
            jacobian = labByXyz * xyzByCoefficients;

            return Vector3.Dot(residual, residual);
        }

        /**
         * <summary>
         * Converts XYZ to CIELAB relative to D65, with its derivative.
         * </summary>
         * <param name="xyz">The color in XYZ.</param>
         * <param name="labByXyz">d lab / d xyz, rows L a b and columns X Y Z.</param>
         * <returns>The color in CIELAB.</returns>
         */
        private static Vector3 Lab(Vector3 xyz, out Matrix4x4 labByXyz)
        {
            Vector3 f = Vector3.zero, slope = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                float t = xyz[axis] / White[axis];

                // a cube root for most colors and a straight line near black where the root gets too steep
                // we do this because the exact cube root stops being differentiable at zero, which makes the jacobian blow up for black
                if (t > LabDelta * LabDelta * LabDelta)
                {
                    f[axis] = Mathf.Pow(t, 1f / 3f);
                    slope[axis] = 1f / (3f * f[axis] * f[axis] * White[axis]);
                }
                else
                {
                    f[axis] = t / (3f * LabDelta * LabDelta) + 4f / 29f;
                    slope[axis] = 1f / (3f * LabDelta * LabDelta * White[axis]);
                }
            }

            // L follows Y alone, a compares X with Y, b compares Y with Z
            labByXyz = new Matrix4x4
            {
                m00 = 0f,                  m01 = 116f * slope.y,     m02 = 0f,
                m10 = 500f * slope.x,      m11 = -500f * slope.y,    m12 = 0f,
                m20 = 0f,                  m21 = 200f * slope.y,     m22 = -200f * slope.z,

                m33 = 1f
            };


            return new Vector3(116f * f.y - 16f, 500f * (f.x - f.y), 200f * (f.y - f.z));
        }

        /**
         * <summary>
         * The derivative of the sigmoid.
         * </summary>
         * <param name="x">The number.</param>
         * <returns>The sigmoid's slope at x.</returns>
         */
        private static float SigmoidSlope(float x)
        {
            float root = Mathf.Sqrt(1f + x * x);
            return 0.5f / (root * root * root);
        }

        /**
         * <summary>
         * Builds the weights for each wavelength, which is D65 times the matching functions, scaled so D65 itself has Y = 1.
         * </summary>
         * <returns>The weights for each wavelength.</returns>
         */
        private static Vector3[] BuildWeights()
        {
            var weights = new Vector3[Cie.SampleCount];
            float whiteY = 0f;

            for (int row = 0; row < Cie.SampleCount; row++)
            {
                float lambda = Cie.FirstWavelength + row;
                weights[row] = Cie.D65At(lambda) * Cie.MatchingFunctions(lambda);
                whiteY += weights[row].y;
            }

            // dividing by D65's own Y makes a reflectance of 1 everywhere come out as sRGB white
            for (int row = 0; row < Cie.SampleCount; row++)
                weights[row] /= whiteY;


            return weights;
        }

        /**
         * <summary>
         * Sums an array of vectors.
         * </summary>
         * <param name="values">The vectors.</param>
         * <returns>The sum.</returns>
         */
        private static Vector3 Sum(Vector3[] values)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 value in values)
                sum += value;

            return sum;
        }

        #endregion
    }
}

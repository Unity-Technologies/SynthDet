﻿/* ===================================================
 *  purpose:	sample using the free method for
 *              spiral computation
 * ---------------------------------------------------
 *  using methods of CEPHES library
 * ---------------------------------------------------
 *  first edit:	09.03.2010 by M. Dupuis @ VIRES GmbH
 *  last mod.:  09.03.2010 by M. Dupuis @ VIRES GmbH
 * ===================================================
    Copyright 2010 VIRES Simulationstechnologie GmbH

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
    
    Ported to C# by Unity Technologies
 */

using System;
using Unity.Mathematics;
using UnityEngine;

namespace UnityEngine.SimViz.Content.Sampling
{
    internal static class Spiral
    {
        /* S(x) for small x */
        static readonly double[] sn = new double[6] {
            -2.99181919401019853726E3,
            7.08840045257738576863E5,
            -6.29741486205862506537E7,
            2.54890880573376359104E9,
            -4.42979518059697779103E10,
            3.18016297876567817986E11,
        };

        static readonly double[] sd = new double[6] {
            /* 1.00000000000000000000E0,*/
            2.81376268889994315696E2,
            4.55847810806532581675E4,
            5.17343888770096400730E6,
            4.19320245898111231129E8,
            2.24411795645340920940E10,
            6.07366389490084639049E11,
        };

        /* C(x) for small x */
        static readonly double[] cn = new double[6] {
            -4.98843114573573548651E-8,
            9.50428062829859605134E-6,
            -6.45191435683965050962E-4,
            1.88843319396703850064E-2,
            -2.05525900955013891793E-1,
            9.99999999999999998822E-1,
        };

        static readonly double[] cd = new double[7] {
            3.99982968972495980367E-12,
            9.15439215774657478799E-10,
            1.25001862479598821474E-7,
            1.22262789024179030997E-5,
            8.68029542941784300606E-4,
            4.12142090722199792936E-2,
            1.00000000000000000118E0,
        };

        /* Auxiliary function f(x) */
        static readonly double[] fn = new double[10] {
            4.21543555043677546506E-1,
            1.43407919780758885261E-1,
            1.15220955073585758835E-2,
            3.45017939782574027900E-4,
            4.63613749287867322088E-6,
            3.05568983790257605827E-8,
            1.02304514164907233465E-10,
            1.72010743268161828879E-13,
            1.34283276233062758925E-16,
            3.76329711269987889006E-20,
        };

        static readonly double[] fd = new double[10] {
            /*  1.00000000000000000000E0,*/
            7.51586398353378947175E-1,
            1.16888925859191382142E-1,
            6.44051526508858611005E-3,
            1.55934409164153020873E-4,
            1.84627567348930545870E-6,
            1.12699224763999035261E-8,
            3.60140029589371370404E-11,
            5.88754533621578410010E-14,
            4.52001434074129701496E-17,
            1.25443237090011264384E-20,
        };

        /* Auxiliary function g(x) */
        static readonly double[] gn = new double[11] {
            5.04442073643383265887E-1,
            1.97102833525523411709E-1,
            1.87648584092575249293E-2,
            6.84079380915393090172E-4,
            1.15138826111884280931E-5,
            9.82852443688422223854E-8,
            4.45344415861750144738E-10,
            1.08268041139020870318E-12,
            1.37555460633261799868E-15,
            8.36354435630677421531E-19,
            1.86958710162783235106E-22,
        };

        static readonly double[] gd = new double[11] {
            /*  1.00000000000000000000E0,*/
            1.47495759925128324529E0,
            3.37748989120019970451E-1,
            2.53603741420338795122E-2,
            8.14679107184306179049E-4,
            1.27545075667729118702E-5,
            1.04314589657571990585E-7,
            4.60680728146520428211E-10,
            1.10273215066240270757E-12,
            1.38796531259578871258E-15,
            8.39158816283118707363E-19,
            1.86958710162783236342E-22,
        };

        static double polevl(double x, double[] coef, int n)
        {
            double ans;
            int i, j;

            i = n;
            j = 0;
            ans = coef[j++];

            do
            {
                ans = ans * x + coef[j++];
            }
            while ((--i) > 0);

            return ans;
        }

        static double p1evl(double x, double[] coef, int n)
        {
            double ans;
            int i, j;

            i = n - 1;
            j = 0;
            ans = x + coef[j++];
        

            do
            {
                ans = ans * x + coef[j++];
            }
            while ((--i) > 0);

            return ans;
        }


        static void fresnel(double xxa, out double ssa, out double cca)
        {
            double f, g, cc, ss, c, s, t, u;
            double x, x2;

            x = Math.Abs(xxa);
            x2 = x * x;

            if (x2 < 2.5625)
            {
                t = x2 * x2;
                ss = x * x2 * polevl(t, sn, 5) / p1evl(t, sd, 6);
                cc = x * polevl(t, cn, 5) / polevl(t, cd, 6);
            }
            else if (x > 36974.0)
            {
                cc = 0.5;
                ss = 0.5;
            }
            else
            {
                x2 = x * x;
                t = Math.PI * x2;
                u = 1.0 / (t * t);
                t = 1.0 / t;
                f = 1.0 - u * polevl(u, fn, 9) / p1evl(u, fd, 10);
                g = t * polevl(u, gn, 10) / p1evl(u, gd, 11);

                t = Math.PI * 0.5 * x2;
                c = Math.Cos(t);
                s = Math.Sin(t);
                t = Math.PI * x;
                cc = 0.5 + (f * s - g * c) / t;
                ss = 0.5 - (f * c + g * s) / t;
            }

            if (xxa < 0.0)
            {
                cc = -cc;
                ss = -ss;
            }

            cca = cc;
            ssa = ss;
        }

        /// <summary>
        /// compute the actual "standard" spiral, starting with curvature 0 
        /// </summary>
        /// <param name="s">run-length along spiral</param>
        /// <param name="cDot">first derivative of curvature [1/m2]</param>
        /// <param name="x">resulting x-coordinate in spirals local co-ordinate system [m]</param>
        /// <param name="y">resulting y-coordinate in spirals local co-ordinate system [m]</param>
        /// <param name="t">tangent direction at s [rad]</param>
        public static void odrSpiral(double s, double cDot, out double x, out double y, out double t)
        {
            double a;

            a = 1.0 / Math.Sqrt(Math.Abs(cDot));
            a *= Math.Sqrt(Math.PI);

            fresnel(s / a, out x, out y);

            x *= a;
            y *= a;

            if (cDot < 0.0)
                y *= -1.0;

            t = s * s * cDot * 0.5;
        }

        public static PointSampleLocal Compute(float s, float curvatureStart, float curvatureEnd, float length)
        {
            var dCurvature = curvatureEnd - curvatureStart;
            var curvatureSlope = dCurvature / length;
            var startU = curvatureStart / curvatureSlope;
        
            odrSpiral(s + startU, curvatureSlope, out var x, out var y, out var t);
            var samplePosition = new Vector2((float)x, (float)y);
        
            //translate to make the start point the origin
            odrSpiral(startU, curvatureSlope, out var xStart, out var yStart, out var tStart);
            var startPosition = new Vector2((float)xStart, (float)yStart);

            samplePosition -= startPosition;
       
            //To ensure that positive curvature tends to the right and negative to the left,
            //flip the coordinate if curvatureStart > curvatureEnd
            if (curvatureStart > curvatureEnd)
                samplePosition = -samplePosition;
            
            //Rotate to make the initial heading (tStart) line up with (0,1)
            var unrotate = Quaternion.AngleAxis((float)-tStart * Mathf.Rad2Deg, Vector3.back);
            samplePosition = unrotate * samplePosition;
            t -= tStart;
            // Flip to inertial coordinates...
            var point = new float2(samplePosition.y, samplePosition.x);
        
            return new PointSampleLocal(point, (float)t);
        }
    }
}

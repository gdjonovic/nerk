using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace FaceTrackingBasics
{
    class CoefficientCalculatorUtil
    {

        const double Rad2Deg = 180.0 / Math.PI;
        const double Deg2Rad = Math.PI / 180.0;

        /// <summary>
        /// Calculates angle in radians between two points and x-axis.
        /// </summary>
        public static double CalculateAngle(Point start, Point end)
        {
            if (Math.Atan2(start.Y - end.Y, end.X - start.X) > 1)
            {
                return Math.Round(1/Math.Atan2(start.Y - end.Y, end.X - start.X),1);
            }
            else
            {
                return Math.Round(Math.Atan2(start.Y - end.Y, end.X - start.X),1);
            }
        }


        public static Dictionary<String,double> CalculateRegionMoments(List<Point> points)
        {


            double xM = 0;
            double yM = 0;
            int n = 0;

            double c20 = 0;
            double c11 = 0;
            double c02 = 0;

            double theta = 0;
            double eccentricity = 0;
            double majorAxisLength = 0;
            double minorAxisLength = 0;


            Dictionary<String, double> resultParams = new Dictionary<String, double>();

            n = points.Count;
            foreach (Point pp in points)
            {
                xM += pp.X;
                yM += pp.Y;

            }
            xM = xM / n;
            yM = yM / n;
            foreach (Point pp in points)
            {
                c20 += (pp.X - xM) * (pp.X - xM);
                c11 += (pp.X - xM) * (pp.Y - yM);
                c02 += (pp.Y - yM) * (pp.Y - yM);
            }
            // sad imamo vrednosti covariance matrix
            // c20 c11
            // c11 c02
            // odrediti karakteristicne vrednosti
            double a = 1;
            double b = -(c20 + c02);
            double c = c20 * c02 - c11 * c11;
            double D = b * b - 4 * c;
            double alfa1 = 0;
            double alfa2 = 0;
            if (D > 0)
            {
                D = Math.Sqrt(D);
                alfa1 = (-b + D) / 2 * a;
                alfa2 = (-b - D) / 2 * a;
                double temp1 = Math.Max(alfa1, alfa2);
                double temp2 = Math.Min(alfa1, alfa2);
                alfa1 = temp1;
                alfa2 = temp2;
                if (alfa1 != 0)
                    eccentricity = alfa2 / alfa1;// Math.Sqrt(1 - alfa2 / alfa1);
                majorAxisLength = alfa1;
                minorAxisLength = alfa2;
            }
            // odrediti karakteristicni vektor
            // C*kV = alfa*kV
            theta = 0.5 * Math.Atan2(2 * c11, (c20 - c02));

            resultParams.Add("theta", theta);
            resultParams.Add("eccentricity", Math.Round(eccentricity,1));

            return resultParams;
        }

        

       

    }
}

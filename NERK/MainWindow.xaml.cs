// -----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace FaceTrackingBasics
{
    using System;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Windows.Shapes;
    using Microsoft.Kinect.Toolkit;
    using System.Windows.Controls;
    using System.Collections.Generic;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly int Bgr32BytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;
        private readonly KinectSensorChooser sensorChooser = new KinectSensorChooser();
        private WriteableBitmap colorImageWritableBitmap;
        private byte[] colorImageData;
        private ColorImageFormat currentColorImageFormat = ColorImageFormat.Undefined;
        private KinectSensor newSensor;
        private Boolean isRecordingOn = false;
        private TrainingData trainingData = TrainingData.Instance();
        private NeuralNetwork nn = NeuralNetwork.Instance();

        public MainWindow()
        {
            InitializeComponent();

            var faceTrackingViewerBinding = new Binding("Kinect") { Source = sensorChooser };
            faceTrackingViewer.SetBinding(FaceTrackingViewer.KinectProperty, faceTrackingViewerBinding);

            sensorChooser.KinectChanged += SensorChooserOnKinectChanged;

            sensorChooser.Start();
            List<double[]> smileData = trainingData.Load("smileNERK");
            foreach (double[] inputs in smileData)
            {
                nn.SetInput(inputs);
                nn.trainNetwork(1);
            }
            List<double[]> sadData = trainingData.Load("sadNERK");
            foreach (double[] inputs in sadData)
            {
                nn.SetInput(inputs);
                nn.trainNetwork(2);
            }
            List<double[]> neutralData = trainingData.Load("neutralNERK");
            foreach (double[] inputs in neutralData)
            {
                nn.SetInput(inputs);
                nn.trainNetwork(3);
            }
            
        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs kinectChangedEventArgs)
        {
            KinectSensor oldSensor = kinectChangedEventArgs.OldSensor;
             newSensor = kinectChangedEventArgs.NewSensor;

            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= KinectSensorOnAllFramesReady;
                oldSensor.ColorStream.Disable();
                oldSensor.DepthStream.Disable();
                oldSensor.DepthStream.Range = DepthRange.Default;
                oldSensor.SkeletonStream.Disable();
                oldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                oldSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                oldSensor.SkeletonFrameReady += this.SensorSkeletonFrameReady; 
            }

            if (newSensor != null)
            {
                try
                {
                    newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                    newSensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                    try
                    {
                        // This will throw on non Kinect For Windows devices.
                        newSensor.DepthStream.Range = DepthRange.Near;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        newSensor.DepthStream.Range = DepthRange.Default;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }

                    newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                    newSensor.SkeletonStream.Enable();
                    newSensor.AllFramesReady += KinectSensorOnAllFramesReady;
                    newSensor.SkeletonFrameReady += this.SensorSkeletonFrameReady; 

                }
                catch (InvalidOperationException)
                {
                    // This exception can be thrown when we are trying to
                    // enable streams on a device that has gone away.  This
                    // can occur, say, in app shutdown scenarios when the sensor
                    // goes away between the time it changed status and the
                    // time we get the sensor changed notification.
                    //
                    // Behavior here is to just eat the exception and assume
                    // another notification will come along if a sensor
                    // comes back.
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            if (skeletons.Length != 0)
            {
                foreach (Skeleton skel in skeletons)
                {
                    this.AnalizeFace(skel);
                }
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void AnalizeFace(Skeleton skeleton)
        {
            double[] inputs = new double[9];
            // inputs[0] jawLowerer
            // inputs[1] eyeBrow
            // inputs[2] lipCornerDepressor
            // inputs[3] upperLipRaiser
            // inputs[4] lipStrecher
            // inputs[5] OuterEyeBrowRaiser
            // inputs[6] lefteyeEccentricity
            // inputs[7] righteyeEccentricity
            // inputs[8] mouthEccentricity

           
            double pixelMeterRatio = CalculatePixelMeterRatio(skeleton);

            if (faceTrackingViewer.GetFaceWidth() > 0 && pixelMeterRatio > 0)
            {
                FaceWidth.Text = "Face width: " + Math.Round((faceTrackingViewer.GetFaceWidth() * pixelMeterRatio) * 100, 1).ToString() + " cm";
                FaceLenght.Text = "Face lenght: " + Math.Round((faceTrackingViewer.GetFaceLenght() * pixelMeterRatio) * 100, 1).ToString() + " cm";
                JawLowerer.Text = "JawLowerer: " + FaceTrackingViewer.jawLowerer;
                inputs[0] = Math.Round(FaceTrackingViewer.jawLowerer, 1);
                EyeBrow.Text = "EyeBrow: " + FaceTrackingViewer.eyeBrow;
                inputs[1] = Math.Round(FaceTrackingViewer.eyeBrow, 1);
                LipCornerDepressor.Text = "LipCornerDepressor: " + FaceTrackingViewer.lipCornerDepressor;
                inputs[2] = Math.Round(FaceTrackingViewer.lipCornerDepressor, 1);
                UpperLipRaiser.Text = "UpperLipRaiser: " + FaceTrackingViewer.upperLipRaiser;
                inputs[3] = Math.Round(FaceTrackingViewer.upperLipRaiser, 1);
                LipStrecher.Text = "LipStrecher: " + FaceTrackingViewer.lipStrecher;
                inputs[4] = Math.Round(FaceTrackingViewer.lipStrecher, 1);
                OuterBrowraiser.Text = "OuterBrowraiser: " + FaceTrackingViewer.outerBrowraiser;
                inputs[5] = Math.Round(FaceTrackingViewer.outerBrowraiser, 1);
                if (FaceTrackingViewer.isLeftEyeOn)
                {
                   Dictionary<String, double> resultParams = CoefficientCalculatorUtil.CalculateRegionMoments(FaceTrackingViewer.leftEyePoints2D);
                   lbLeftEyeEccentricity.Content = resultParams["eccentricity"];
                   inputs[6] = resultParams["eccentricity"];
                   lbLeftEyeInnerAngle.Content = CoefficientCalculatorUtil.CalculateAngle(FaceTrackingViewer.leftEyeCenter, FaceTrackingViewer.innerLeftEyeBrow);
                   lbLeftEyeOuterAngle.Content = CoefficientCalculatorUtil.CalculateAngle(FaceTrackingViewer.leftEyeCenter, FaceTrackingViewer.outerLeftEyeBrow);
                }

                if (FaceTrackingViewer.isRightEyeOn)
                {
                    Dictionary<String, double> resultParams = CoefficientCalculatorUtil.CalculateRegionMoments(FaceTrackingViewer.rightEyePoints2D);
                    lbRightEyeEccentricity.Content = resultParams["eccentricity"];
                    inputs[7] = resultParams["eccentricity"];
                    lbRightEyeInnerAngle.Content = CoefficientCalculatorUtil.CalculateAngle(FaceTrackingViewer.rightEyeCenter, FaceTrackingViewer.innerRightEyeBrow);
                    lbRightEyeOuterAngle.Content = CoefficientCalculatorUtil.CalculateAngle(FaceTrackingViewer.rightEyeCenter, FaceTrackingViewer.outerRightEyeBrow);
                }

                if (FaceTrackingViewer.isMouthOn)
                {
                    Dictionary<String, double> resultParams = CoefficientCalculatorUtil.CalculateRegionMoments(FaceTrackingViewer.mountPoints2D);
                    lbMouthEyeEccentricity.Content = resultParams["eccentricity"];
                    inputs[8] = resultParams["eccentricity"];
                    lbMouthLeftCornerAngle.Content = CoefficientCalculatorUtil.CalculateAngle(FaceTrackingViewer.chinCenter, FaceTrackingViewer.leftMouthCorner);
                    lbMouthRightCornerAngle.Content = CoefficientCalculatorUtil.CalculateAngle(FaceTrackingViewer.chinCenter, FaceTrackingViewer.rightMouthCorner);
                }

                if (isRecordingOn)
                {
                    trainingData.Add(inputs);
                }
                else
                {
                    nn.SetInput(inputs);
                    nn.recognizeEmotion();
                    if(nn.Index == 1){
                        lbRecognizedEmotion.Content = "Smile";
                    }else if(nn.Index == 2){
                        lbRecognizedEmotion.Content = "Surprise";
                    }else if (nn.Index == 3){
                        lbRecognizedEmotion.Content = "Neutral";
                    }else{
                    }
                    
                }
            }

        }

        private double CalculatePixelMeterRatio(Skeleton skeleton)
        {
            double inPixels = SkeletonPointToScreen(skeleton.Joints[JointType.ShoulderCenter].Position).Y - SkeletonPointToScreen(skeleton.Joints[JointType.Head].Position).Y;
            double inMetres = Length(skeleton.Joints[JointType.Head], skeleton.Joints[JointType.ShoulderCenter]);

            if (inMetres > 0 && inPixels > 0)
            {
                return inMetres / inPixels;
            }
            else
            {
                return 0;
            }
        }

        public static double Length(Joint p1, Joint p2)
        {
            return Math.Sqrt(
                Math.Pow(p1.Position.X - p2.Position.X, 2) +
                Math.Pow(p1.Position.Y - p2.Position.Y, 2) +
                Math.Pow(p1.Position.Z - p2.Position.Z, 2));
        }


        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.newSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            sensorChooser.Stop();
            faceTrackingViewer.Dispose();
        }

        private void KinectSensorOnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            using (var colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame())
            {
                if (colorImageFrame == null)
                {
                    return;
                }

                // Make a copy of the color frame for displaying.
                var haveNewFormat = this.currentColorImageFormat != colorImageFrame.Format;
                if (haveNewFormat)
                {
                    this.currentColorImageFormat = colorImageFrame.Format;
                    this.colorImageData = new byte[colorImageFrame.PixelDataLength];
                    this.colorImageWritableBitmap = new WriteableBitmap(
                        colorImageFrame.Width, colorImageFrame.Height, 96, 96, PixelFormats.Bgr32, null);
                    ColorImage.Source = this.colorImageWritableBitmap;
                }

                colorImageFrame.CopyPixelDataTo(this.colorImageData);
                this.colorImageWritableBitmap.WritePixels(
                    new Int32Rect(0, 0, colorImageFrame.Width, colorImageFrame.Height),
                    this.colorImageData,
                    colorImageFrame.Width * Bgr32BytesPerPixel,
                    0);
            }
        }

        private void cbLeftEye_Click(object sender, RoutedEventArgs e)
        {
            FaceTrackingViewer.isLeftEyeOn = (bool)cbLeftEye.IsChecked;
        
        }

        private void cbRightEye_Click(object sender, RoutedEventArgs e)
        {
            FaceTrackingViewer.isRightEyeOn = (bool)cbRightEye.IsChecked;
        }

        private void cbMouth_Click(object sender, RoutedEventArgs e)
        {
            FaceTrackingViewer.isMouthOn = (bool)cbMouth.IsChecked;
        }

        private void cbTopHead_Click(object sender, RoutedEventArgs e)
        {
            FaceTrackingViewer.isTopHeadOn = (bool)cbMouth.IsChecked;
        }

        private void cbChin_Click(object sender, RoutedEventArgs e)
        {
            FaceTrackingViewer.isChinOn = (bool)cbChin.IsChecked;
        }

        private void btnRecordData_Click(object sender, RoutedEventArgs e)
        {
            if (isRecordingOn)
            {
                //stop recording
                isRecordingOn = false;
                btnRecordData.Content = "Record data";
                trainingData.Save();
            }
            else
            {
                isRecordingOn = true;
                btnRecordData.Content = "Stop recording";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FaceTrackingBasics
{
    public class NeuralNetwork
    {
        private double[][] weights;
        private double[] input;
        private double[] output;

        private int index; //emotion index

        private bool initialized = false;
        private double match;

        private const int numberOfOutputs = 3;//three outpus for facial expressions 1. happy
                                                                                  //2. sad
                                                                                  //3. neutral


        public NeuralNetwork()
        {
        }

        private static NeuralNetwork nn = new NeuralNetwork();
        public static NeuralNetwork Instance()
        {
            return nn;
        }

        public void SetInput(double[] input)
        {
            if (!initialized)
            {
                initialized = true;

                weights = new double[input.Length][];

                for (int i = 0; i < input.Length; i++)
                {
                    weights[i] = new double[numberOfOutputs];
                }
            }
            this.input = new double[input.Length];
            this.input = input;
        }


        public void trainNetwork(int emotionIndex)
        {
            double reward = 0.0;
            double punishment = 0.0;

            for (int j = 0; j < input.Length; j++)
            {
                if (input[j] > 0.3)
                    reward++;
                else
                    punishment++;
            }
            if (reward != 0)
                reward = 1.0 / reward;
            if (punishment != 0)
                punishment = -1.0 / punishment;

            for (int j = 0; j < input.Length; j++)
            {
                if (input[j] > 0.3)
                    weights[j][emotionIndex - 1] = reward;
                else
                    weights[j][emotionIndex - 1] = punishment;
            }

        }

        public void recognizeEmotion()
        {
            output = new double[numberOfOutputs];
            int max = 0;
            for (int i = 0; i < numberOfOutputs; i++)
            {
                double net = 0.0;
                for (int j = 0; j < input.Length; j++)
                {
                    net += weights[j][i] * input[j];
                }
                output[i] = activating(net);
                if (output[i] > output[max] || i == 0)
                {
                    max = i;
                    match = net * 100;
                }
            }
            index = max;
            match = Math.Round(match); //matching %
        }

        double activating(double d)
        {
            double d1 = (d + 1.0) / 2;
            if (d > 1.0)
                d1 = 1.0;
            if (d < -1.0)
                d1 = 0.0;
            return d1;
        }


        #region Index Property
        public int Index
        {
            get { return index; }
            set { this.index = Index; }
        }
        #endregion

        #region Match Property
        public double Match
        {
            get { return match; }
        }
        #endregion

    }
}

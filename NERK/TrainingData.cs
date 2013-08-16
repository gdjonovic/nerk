using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace FaceTrackingBasics
{
    [Serializable]
    class TrainingData
    {
        private List<double[]> trainingData = new List<double[]>();

        private static TrainingData tData = new TrainingData();

        public static TrainingData Instance()
        {
            return tData;
        }

        

        public void Save()
        {
            TrainingData eviden = TrainingData.Instance();
            IFormatter formatter = new BinaryFormatter();
            Stream stream = new FileStream("testDataNERK", FileMode.Create, FileAccess.Write, FileShare.None);
            formatter.Serialize(stream, eviden);
            stream.Close();

        }

        public void Add(double[] inputs)
        {
            trainingData.Add(inputs);
        }


        public List<double[]> Load(string filename)
        {
            IFormatter formatter = new BinaryFormatter();
            TrainingData evid = null;
            Stream stream = null;

            try
            {
                stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException ex)
            {
               // MessageBox.Show("Nije moguće otvoriti datoteku zbog " + ex + "!", "Greška!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
                if (stream != null)
                    evid = (TrainingData)formatter.Deserialize(stream);
            }
            catch (SerializationException ex)
            {
                //MessageBox.Show("Nije moguće otvoriti datoteku zbog " + ex + "!", "Greška!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
            if (evid == null)
            {
                return new List<double[]>();

            }
            else
            {
                return evid.trainingData;

            }

        }
    }
}
﻿using System;
using System.IO;

namespace SDSSoundEncrypt
{
    /// <summary>
    /// Stochastic differential system used for encryption. 
    /// </summary>
    class SDS
    {
        // A lot of "magic numbers" - they provide stable mathematical model
        // SDS parameters
        private const double B1 = 0.02;
        private const double B2 = 2.0;
        private const double C1 = 1.0;
        private const double C5 = 0.5;
        // N0 - white noise intensity
        private const double N0 = 0.0004;
        // DT - time delta
        private const double DT = 0.1;
        // SDS default starting values
        private const double X10 = 0.1;
        private const double X20 = 0;
        // Modificator to convert 16-bit samples signed integers [-32768; 32767] to [-1; 1] range
        private const double Modif = 32768;
        // For Z calculation
        private const double Z1 = -5.0;
        private const double Z2 = 10.0;

        /// <summary>
        /// Calculates SDS x1 member.
        /// </summary>
        /// <param name="x1Prev">x1 value from previous step</param>
        /// <param name="x2Prev">x2 value from previous step</param>
        /// <returns>Calculated x1 SDS value.</returns>
        private static double GetX1Encrypted(double x1Prev, double x2Prev)
        {
            return x1Prev + x2Prev * DT;
        }

        /// <summary>
        /// Calculates SDS x2 member.
        /// </summary>
        /// <param name="c3">c3 input parameter (in our case - sound sample)</param>
        /// <param name="x1Prev">x1 value from previous step</param>
        /// <param name="x2Prev">x2 value from previous step</param>
        /// <param name="nPrev">n (white noise) value from previous step</param>
        /// <returns>Calculates x2 SDS value.</returns>
        private static double GetX2Encrypted(double c3, double x1Prev, double x2Prev, double nPrev)
        {
            // Using c5
            return x2Prev + (nPrev - B1 * x2Prev - B2 * x2Prev * Math.Abs(x2Prev) - C1 * x1Prev - c3 * Math.Pow(x1Prev, 3) - C5 * Math.Pow(x1Prev, 5)) * DT;
            //return x2Prev + (nPrev - B1 * x2Prev - B2 * x2Prev * Math.Abs(x2Prev) - C1 * x1Prev - c3 * Math.Pow(x1Prev, 3)) * DT;
        }

        /// <summary>
        /// Reverse calculation: c3 value is decrypted sound sample.
        /// </summary>
        /// <param name="x1i">x1[i] value</param>
        /// <param name="x1i1">x1[i+1] (next) value</param>
        /// <param name="x1i2">x1[i+2] value (after next)</param>
        /// <param name="n">n (white noise) value</param>
        /// <returns>Calculates c3 (SDS parameter) reverse value.</returns>
        private static double GetC3Decrypted(double x1i, double x1i1, double x1i2, double n)
        {
            // Using c5
            return ((2 * x1i1 - x1i - x1i2) / (DT * DT) + n - C1 * x1i - C5 * Math.Pow(x1i, 5) - B1 * (x1i1 - x1i) / DT - B2 * (x1i1 - x1i) * Math.Abs(x1i1 - x1i) / (DT * DT)) / Math.Pow(x1i, 3);
            //return ((2 * x1i1 - x1i - x1i2) / (DT * DT) + n - C1 * x1i - B1 * (x1i1 - x1i) / DT - B2 * (x1i1 - x1i) * Math.Abs(x1i1 - x1i) / (DT * DT)) / Math.Pow(x1i, 3);
        }

        /// <summary>
        /// Encrypts WAV file with SDS.
        /// </summary>
        /// <param name="fileName">Original file name.</param>
        /// <param name="fileNameEncrypted">Encrypted file name.</param>
        /// <param name="key">Encryption key - seed value for random number generator (same value should be used for decryption).</param>
        public static void EncryptWAVFile(string fileName, string fileNameEncrypted, int key)
        {
            // SDS values and white noise
            double x1, x2, n;
            // SDS values from previous step, required for calculations
            double x1Prev, x2Prev, nPrev;
            // Normally distributed random variable z and SDS input parameter c3 - normalized sample
            double z, c3;
            // Sound sample from WAV file
            short sample;

            // Seed a new random (seed value is our key)
            Random rnd = new Random(key);

            // Filling starting values
            z = Z1 + Z2 * rnd.NextDouble();
            nPrev = z * Math.Sqrt(N0 / DT);
            x1Prev = X10;
            x2Prev = X20;

            FileStream fsOriginal = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fsOriginal);

            // Move to data position right after header (header size is 44 bytes)
            br.BaseStream.Position = 44;

            FileStream fsEncrypted = new FileStream(fileNameEncrypted, FileMode.Append, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fsEncrypted);

            // x1 starting value is written as the first sample into encrypted file
            bw.Write(x1Prev);

            while (br.BaseStream.Length > br.BaseStream.Position)
            {
                // Read 16-bit sample from WAV file
                sample = br.ReadInt16();

                // DEBUG INFO - samples with max values [-32768; 32767] sometimes cause problems
                /*if ((Math.Abs((int)sample) == Modif) || (Math.Abs((int)sample) == (Modif - 1)))
                    Console.WriteLine(sample);*/

                // Normalize sample to [-1; 1]
                c3 = (double)sample / Modif;

                // Calculate white noise and SDS values
                z = Z1 + Z2 * rnd.NextDouble();
                n = z * Math.Sqrt(N0 / DT);
                x1 = GetX1Encrypted(x1Prev, x2Prev);
                x2 = GetX2Encrypted(c3, x1Prev, x2Prev, nPrev);

                // x1 is the encrypted sample so it's written to file
                bw.Write(x1);

                // Set current values as previous for the next iteration
                x1Prev = x1;
                x2Prev = x2;
                nPrev = n;
            }

            Console.WriteLine("Finished encrypting.");

            br.Close();
            fsOriginal.Close();
            bw.Close();
            fsEncrypted.Close();
        }

        /// <summary>
        /// Decrypts WAV file with SDS.
        /// </summary>
        /// <param name="fileNameEncrypted">Encrypted file name.</param>
        /// <param name="fileNameDecrypted">Decrypted file name.</param>
        /// <param name="key">Decryption key - seed value for random number generator (same value should be used for encryption).</param>
        public static void DecryptWAVFile(string fileNameEncrypted, string fileNameDecrypted, int key)
        {
            // SDS values x1[i], x1[i+1], x1[i+2], c3 (contains encrypted sample value)
            double x1i, x1i1, x1i2, c3;
            // Normally distributed random variable z and white noise value n
            double z, n;
            // Decrypted sample value
            short sample;

            // Seed a new random (seed value is our key)
            Random rnd = new Random(key);

            FileStream fsEncrypted = new FileStream(fileNameEncrypted, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fsEncrypted);

            // Move to data position right after header (header size is 44 bytes)
            br.BaseStream.Position = 44;

            FileStream fsDecrypted = new FileStream(fileNameDecrypted, FileMode.Append, FileAccess.Write);
            BinaryWriter bw = new BinaryWriter(fsDecrypted);

            while (br.BaseStream.Length > br.BaseStream.Position)
            {
                // We need to read 3 consecutive SDS values x1[i], x1[i+1] and x1[i+2]
                x1i = br.ReadDouble();
                // We could reach EOF after reading
                if (!(br.BaseStream.Length > br.BaseStream.Position))
                    break;
                x1i1 = br.ReadDouble();
                // We could reach EOF after reading
                if (!(br.BaseStream.Length > br.BaseStream.Position))
                    break;
                x1i2 = br.ReadDouble();

                // Move 2 double positions back because we have read 3 values instead of 1
                br.BaseStream.Seek(-2 * sizeof(double), SeekOrigin.Current);

                // Calculate white noise value
                z = Z1 + Z2 * rnd.NextDouble();
                n = z * Math.Sqrt(N0 / DT);

                // Calculate decrypted c3 parameter value
                c3 = GetC3Decrypted(x1i, x1i1, x1i2, n);

                // Denormalize c3 value from [-1; 1] range to [-32768; 32767]
                c3 *= Modif;            

                // Convert to 16-bit sample
                sample = (short)c3;

                // DEBUG INFO - samples with max values [-32768; 32767] sometimes cause problems
                /*if ((Math.Abs((int)sample) == Modif) || (Math.Abs((int)sample) == (Modif - 1)))
                    Console.WriteLine(sample);
                // Should throw an exception as it's out of 'short' range
                sample = Convert.ToInt16(c3);*/

                // Decrypted sample is written to file
                bw.Write(sample);
            }

            Console.WriteLine("Finished decrypting.");

            br.Close();
            fsEncrypted.Close();
            bw.Close();
            fsDecrypted.Close();
        }
    }
}
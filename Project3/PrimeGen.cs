using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;

/*
 * Author: Danny Gardner drg5567
 * Class: CS251
 * Due Date: 10/26/22
 */

namespace PrimeGenerator
{
    class Generator
    {
        ///<summary>
        ///The main method. Processes the program arguments and finds prime numbers when given correct input
        ///</summary>
        //public static void Main(string[] args)
        //{
        //    string usage = "primegen <bits> <count=1>\r\n" +
        //        "- bits - the number of bits of the prime number, this must be a\r\n" +
        //        "multiple of 8, and at least 32 bits.\r\n" +
        //        "- count - the number of prime numbers to generate, defaults to 1";

        //    if (args.Length == 0 || args.Length > 2)
        //    {
        //        Console.WriteLine(usage);
        //        return;
        //    }

        //    int bits = 0;
        //    try
        //    {
        //        bits = Int32.Parse(args[0]);
        //    }
        //    catch
        //    {
        //        Console.WriteLine("The bit length should be an integer!");
        //        return;
        //    }
        //    int count = 1;
        //    if (bits < 32 || bits % 8 != 0)
        //    {
        //        Console.WriteLine("The big length must be a multiple of 8 and at least 32 bits!");
        //        return;
        //    }
        //    if (args.Length == 2)
        //    {
        //        try
        //        {
        //            count = Int32.Parse(args[1]);
        //        }
        //        catch
        //        {
        //            Console.WriteLine("The number of primes to generate must be an integer!");
        //            return;
        //        }
        //    }

        //    Console.WriteLine("Bitlength: " + bits + " bits");

        //    var primer = new PrimeGen(bits, count);
        //    primer.FindPrimes();
        //}
    }

    /// <remarks>
    /// This class represents the generation of prime numbers
    /// </remarks>
    class PrimeGen
    {
        // The big length of the prime numbers to be generated
        private int bitLength;
        // The number of prime numbers to generate
        private int count;
        // A lock to designate the critical area of threading
        static object myLock = new object();

        private BigInteger[] primes;

        public PrimeGen(int bits, int count)
        {
            this.bitLength = bits;
            this.count = count;
            primes = new BigInteger[count];
        }

        /// <summary>
        /// Find a number of prime numbers with a specific bit length
        /// </summary>
        public void FindPrimes()
        {
            int primeCount = 0;
            //var timer = new Stopwatch();
            //timer.Start();

            Parallel.ForEach(RunForever(), (bool item, ParallelLoopState loopState) => 
            {
                var potentialPrime = RandomBigInt(bitLength);
                if (BigIntExtension.IsProbablyPrime(potentialPrime))
                {
                    lock (myLock)
                    {
                        if (!loopState.IsStopped)
                        {
                            //primeCount++;
                            //Console.WriteLine(primeCount + ": " + potentialPrime);
                            primes[primeCount] = potentialPrime;
                            primeCount++;

                            if (primeCount == count)
                            {
                                loopState.Stop();
                            }
                            //else
                            //{
                            //    Console.WriteLine("");
                            //}
                        }
                    }
                }
            });

            //timer.Stop();
            //var elapsed = timer.Elapsed;
            //var totalTime = elapsed.ToString("hh':'mm':'ss':'fffffff");
            //Console.WriteLine("Time to Generate: " + totalTime);
        }

        public BigInteger[] GetPrimes()
        {
            return primes;
        }

        /// <summary>
        /// A enumerable of infinite "true"s for the parallel foreach
        /// </summary>
        /// <returns>True</returns>
        private static IEnumerable<bool> RunForever()
        {
            while (true) yield return true;
        }

        /// <summary>
        /// Generate a random BigInteger using the RandomNumberGenerator class
        /// </summary>
        /// <param name="bitLength">The required length of the prime number</param>
        /// <returns>A random BigInteger</returns>
        private static BigInteger RandomBigInt(int bitLength)
        {
            var random = RandomNumberGenerator.Create();
            var bytes = new byte[bitLength / 8];
            random.GetBytes(bytes);
            var potentialPrime = new BigInteger(bytes);

            if (potentialPrime < BigInteger.Zero)
            {
                potentialPrime = BigInteger.Negate(potentialPrime);
            }
            return potentialPrime;
        }
    }

    /// <remarks>This class represents extension methods of the BigInteger class</remarks>
    static class BigIntExtension
    {
        /// <summary>
        /// Uses the Miller-Rabin algorithm to determine if a BigInteger is a prime number
        /// </summary>
        /// <param name="num">The BigInteger that is being checked for primality</param>
        /// <param name="k">The number of "witnesses" to use for the algorithm</param>
        /// <returns>Whether the BigInteger is composite or probably prime</returns>
        public static Boolean IsProbablyPrime(this BigInteger num, int k = 10)
        {
            var myTuple = ExpandNum(num);
            var r = myTuple.Item1;
            var d = myTuple.Item2;

            for (int i = 0; i < k; i++)
            {
                var lower = (BigInteger)2;
                var upper = num - lower;
                var a = GetRandomInRange(upper, lower);

                var x = BigInteger.ModPow(a, d, num);
                if (x == 1 || x == num - 1)
                    continue;

                for (i = 0; i < r - 1; i++)
                {
                    x = BigInteger.ModPow(x, 2, num);
                    if (x == num - 1)
                    {
                        continue;
                    }
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Breaks a BigInteger into the format 2^r * d + 1 to be used for the Miller-Rabin algorithm
        /// </summary>
        /// <param name="n">The BigInteger to expand</param>
        /// <returns>A tuple representation of the numbers r and d from the number format</returns>
        private static Tuple<int, BigInteger> ExpandNum(BigInteger n)
        {
            var tempN = n - 1;

            // Check if n - 1 is a power of two
            var powerCheck = BigInteger.Log(tempN, 2);
            if (Math.Ceiling(powerCheck) == Math.Floor(powerCheck))
            {
                return Tuple.Create((int)powerCheck, (BigInteger)1);
            }

            var d = tempN;
            while (d % 2 == 0)
            {
                d /= 2;
            }

            var r = 0;
            var tempMult = BigInteger.Pow(2, r) * d;
            while (tempMult != tempN)
            {
                r++;
                tempMult = BigInteger.Pow(2, r) * d;
            }

            return Tuple.Create(r, d);
        }

        /// <summary>
        /// Generate a random number within a certain range of values
        /// </summary>
        /// <param name="upper">The upper range of the BigInteger to be generated</param>
        /// <param name="lower">The lower range of the BigInteger to be generated</param>
        /// <returns>A random BigInteger within the range specified</returns>
        private static BigInteger GetRandomInRange(BigInteger upper, BigInteger lower)
        {
            var random = RandomNumberGenerator.Create();
            byte[] bytes = upper.ToByteArray();
            BigInteger R;

            do
            {
                random.GetBytes(bytes);
                bytes[bytes.Length - 1] &= (byte)0x7F; //force sign bit to positive
                R = new BigInteger(bytes);
            } while (R >= upper || R < lower);

            return R;
        }
    }
}
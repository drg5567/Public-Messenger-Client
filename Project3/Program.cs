/*
 * Author: Danny Gardner drg5567
 * CS251 Project 3
 * 11/18/22
 */

using System.Net;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using PrimeGenerator;

namespace Project3
{
    /// <summary>
    /// This is the entry point of the program
    /// </summary>
    class Program
    {
        /// <summary>
        /// This is the main method of the program. It processes the arguments passed from the command line and determines if it was valid input.
        /// If the input was valid then the program creates a new MessageClient object and calls the appropriate function with arguments
        /// </summary>
        /// <param name="args">The main arguments passed from the command line as strings.</param>
        public static void Main(string[] args)
        {
            string usage = "messenger <option> <other arguments>\n" +
                            "option: this specifies the type of action to perform. Possible options are:\n" +
                            "   keyGen <keysize>: this will generate a keypair of size keysize bits.\n" +
                            "   sendKey <email>: this sends the generated public key to the server with the email address given.\n" +
                            "   getKey <email>: this will retrieve the public key for a particular user.\n" +
                            "   sendMsg <email> <plaintext>: this will encrypt and send a plaintext message with the public key\n" +
                            "   of the person you are sending it to based on their email addres.\n" +
                            "   getMsg <email>: this will retrieve a message for a particular user based on their email";

            if (args.Length < 2 || args.Length > 3)
            {
                Console.WriteLine(usage);
                return;
            }

            MessageClient client = new MessageClient();
            switch (args[0])
            {
                case "keyGen":
                    int keysize = Int32.Parse(args[1]);
                    if (client.KeyGen(keysize))
                    {
                        Console.WriteLine("Public and Private keys created");
                    }
                    return;
                case "getKey":
                    string getEmail = args[1];
                    if (client.GetKey(getEmail))
                    {
                        Console.WriteLine(getEmail + ".key Created");
                    }
                    return;
                case "sendKey":
                    string sendEmail = args[1];
                    if (client.SendKey(sendEmail))
                    {
                        Console.WriteLine("Public key sent for " + sendEmail);
                    }
                    else
                    {
                        Console.WriteLine("Error sending out pubic key for " + sendEmail);
                    }
                    return;
                case "getMsg":
                    string getMsgEmail = args[1];
                    if (!client.GetMsg(getMsgEmail))
                    {
                        Console.WriteLine("Error receiving message!");
                    }
                    return;
                case "sendMsg":
                    string sendMsgEmail = args[1];
                    string sendText = args[2];
                    if (client.SendMsg(sendMsgEmail, sendText))
                    {
                        Console.WriteLine("Message successfully sent to " + sendMsgEmail);
                    }
                    else
                    {
                        Console.WriteLine("Error sending message to " + sendMsgEmail);
                    }
                    return;
                default:
                    Console.WriteLine(usage);
                    return;
            }
        }
    }

    /// <summary>
    /// This class represents a client that sends and receives messages from other clients over the network
    /// </summary>
    class MessageClient
    {
        // Keys and messages were stored on separate servers. Original server links have been removed
        private string keyLink = "";
        private string messageLink = "";
        private HttpClient client;

        public MessageClient()
        {
            client = new HttpClient();
        }

        /// <summary>
        /// This function creates a set of public and private keys for the user and stores them locally in the directory.
        /// </summary>
        /// <param name="keySize">The number of bits to create keys with</param>
        /// <returns>Whether the function was successful.</returns>
        public bool KeyGen(int keySize)
        {
            if (keySize % 8 != 0)
            {
                Console.WriteLine("Key size must be divisible by 8!");
                return false;
            }
            else
            {
                KeyGen keyMaker = new KeyGen(keySize);
                string publicKey = keyMaker.EncodedKey(true);
                string privateKey = keyMaker.EncodedKey(false);
                File.WriteAllText("public.key", publicKey);
                //File.WriteAllText("private.key", privateKey);
                PrivateKey privKey = new PrivateKey(new List<string>(), privateKey);
                var privateJson = JsonConvert.SerializeObject(privKey);
                File.WriteAllText("private.key", privateJson);
                return true;
            }
        }

        /// <summary>
        /// Communicates to the server to get the public key of another user
        /// </summary>
        /// <param name="email">The email of the user that you are getting the private key for.</param>
        /// <returns>Whether the function was successful.</returns>
        public bool GetKey(string email)
        {
            string keyUrl = keyLink + email;
            var reply = ReplyString(keyUrl);
            try
            {
                PublicKey receivedKey = JsonConvert.DeserializeObject<PublicKey>(reply);
                if (File.Exists(email + ".key"))
                {
                    File.Delete(email + ".key");
                }
                File.WriteAllText(email + ".key", receivedKey.Key);

                return true;
            }
            catch (ArgumentNullException err)
            {
                return false;
            }
        }

        /// <summary>
        /// Read your public key from the local directory and send it to the server to receive messages. This function
        /// also adds this email to the private key to keep track of which emails you have a private key for.
        /// </summary>
        /// <param name="email">The email that will be associated with your public key.</param>
        /// <returns>Whether the function was successful.</returns>
        public bool SendKey(string email)
        {
            if (!File.Exists("public.key"))
            {
                Console.WriteLine("No public key exists yet! Key must be generated before being sent out.");
                return false;
            }
            string keyUrl = keyLink + email;
            PublicKey pubKey = new PublicKey(email, File.ReadAllText("public.key"));
            var jsonKey = JsonConvert.SerializeObject(pubKey);
            var content = new StringContent(jsonKey, Encoding.UTF8, "application/json");
            var reply = PutAsync(keyUrl, content).Result;
            if (reply.StatusCode.Equals(HttpStatusCode.BadRequest))
            {
                return false;
            }
            else
            {
                var codedKey = File.ReadAllText("private.key");
                var privKey = JsonConvert.DeserializeObject<PrivateKey>(codedKey);

                if (!privKey.Emails.Contains(email))
                {
                    privKey.AddEmail(email);

                    File.Delete("private.key");
                    var encodedKey = JsonConvert.SerializeObject(privKey);
                    File.WriteAllText("private.key", encodedKey);
                }
                return true;
            }
        }

        /// <summary>
        /// Check if there are any messages for you from the server associated with a given email.
        /// If there is a message available, decrypt the ciphertext and print the message
        /// </summary>
        /// <param name="email">The email you have associated with your private key</param>
        /// <returns>Whether the function was successful.</returns>
        public bool GetMsg(string email)
        {
            try
            {
                var codedKey = File.ReadAllText("private.key");
                var privKey = JsonConvert.DeserializeObject<PrivateKey>(codedKey);
                if (!privKey.Emails.Contains(email))
                {
                    Console.WriteLine("You don't have a private key for this email!");
                    return false;
                }

                string msgUrl = messageLink + email;
                var reply = ReplyString(msgUrl);

                var message = JsonConvert.DeserializeObject<Message>(reply);
                var ciphertext = message.Content;
                if (ciphertext == null)
                {
                    Console.WriteLine("No message received");
                    return false;
                }
                else
                {
                    Byte[] cipherBytes = Convert.FromBase64String(ciphertext);
                    BigInteger cipherBigInt = new BigInteger(cipherBytes);
                    string privateKey = privKey.Key;
                    RSAClient rsa = new RSAClient(privateKey);

                    BigInteger plainBigInt = rsa.RSA(cipherBigInt);
                    Byte[] plainBytes = plainBigInt.ToByteArray();
                    string plaintext = Encoding.UTF8.GetString(plainBytes);

                    Console.WriteLine(plaintext);
                    return true;
                }
            }
            catch (ArgumentNullException err)
            {
                return false;
            }
        }

        /// <summary>
        /// Encrypt a message for another user using RSA and send the ciphertext to the server.
        /// </summary>
        /// <param name="email">The email of the user you are sending a message to.</param>
        /// <param name="plaintext">The content of the message you are encrypting.</param>
        /// <returns>Whether the function was successful.</returns>
        public bool SendMsg(string email, string plaintext)
        {
            if (!File.Exists(email + ".key"))
            {
                Console.WriteLine(email + ".key does not exist!");
                return false;
            }
            string msgUrl = messageLink + email;

            Byte[] textBytes = Encoding.UTF8.GetBytes(plaintext);
            BigInteger textBigInt = new BigInteger(textBytes);
            var pubKey = File.ReadAllText(email + ".key");
            RSAClient keyInfo = new RSAClient(pubKey);

            BigInteger ciphertext = keyInfo.RSA(textBigInt);
            Byte[] cipherBytes = ciphertext.ToByteArray();
            string encodedCiphertext = Convert.ToBase64String(cipherBytes);
            Message message = new Message(email, encodedCiphertext);

            var jsonMessage = JsonConvert.SerializeObject(message);
            var content = new StringContent(jsonMessage, Encoding.UTF8, "application/json");
            var reply = PutAsync(msgUrl, content).Result;
            if (reply.StatusCode.Equals(HttpStatusCode.BadRequest))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Makes a http GET request to the server and parses the response.
        /// </summary>
        /// <param name="url">The url to use for the http request.</param>
        /// <returns></returns>
        private string? ReplyString(string url)
        {
            HttpResponseMessage message = GetAsync(url).Result;
            var statuscode = message.StatusCode;

            if (!statuscode.Equals(HttpStatusCode.OK))
            {
                Console.WriteLine("Error getting http response!");
                return null;
            }

            Stream receiveStream = message.Content.ReadAsStreamAsync().Result;
            StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
            String response = readStream.ReadToEnd();

            return response;
        }

        /// <summary>
        /// Calls the HttpClient GetAsync function
        /// </summary>
        /// <param name="url">The url of the request</param>
        /// <returns>The result of the task.</returns>
        private async Task<HttpResponseMessage> GetAsync(string url)
        {
            return await client.GetAsync(url);
        }

        /// <summary>
        /// Calls the HttpClient PutAsync function
        /// </summary>
        /// <param name="url">The url to send the request to</param>
        /// <param name="content">The content of the request</param>
        /// <returns>The result of the task</returns>
        private async Task<HttpResponseMessage> PutAsync(string url, HttpContent content)
        {
            return await client.PutAsync(url, content);
        }
    }

    /// <summary>
    /// This class generates public/private key pairs and formats them
    /// </summary>
    class KeyGen
    {
        private BigInteger p;
        private BigInteger q;
        private BigInteger N;
        private BigInteger r;
        private BigInteger E;
        private BigInteger D;

        /// <summary>
        /// Generates a public/private keypair using the PrimeGen class
        /// </summary>
        /// <param name="keySize">Number of bits to use for the key</param>
        public KeyGen(int keySize)
        {
            var pBytes = Math.Ceiling(keySize * .4);
            while (pBytes % 8 != 0)
            {
                pBytes += 1;
            }
            var pNum = (int)pBytes;
            var qNum = keySize - pNum;

            PrimeGen pGenerator = new PrimeGen(pNum, 1);
            pGenerator.FindPrimes();
            p = pGenerator.GetPrimes()[0];

            PrimeGen qGenerator = new PrimeGen(qNum, 1);
            qGenerator.FindPrimes();
            q = qGenerator.GetPrimes()[0];

            N = p * q;
            r = (p - 1) * (q - 1);

            E = 65537;
            D = ModInverse(E, r);
        }

        /// <summary>
        /// Returns a base64 encoded string representing an RSA key
        /// </summary>
        /// <param name="isPublic">A boolean indicating whether this key is public or private</param>
        /// <returns>A base64 encoded string representing the key</returns>
        public string EncodedKey(bool isPublic)
        {
            return Convert.ToBase64String(KeyByteArray(isPublic));
        }

        /// <summary>
        /// Formats an RSA key into a byte array in the format xxxxXXXXXXX....XXXnnnnNNNNNNN...NNN
        /// X represents either E or D depending on whether the key is public or private
        /// x indicates the size of X
        /// </summary>
        /// <param name="isPublic">Whether the key is public or private</param>
        /// <returns>A byte array representing the key</returns>
        private Byte[] KeyByteArray(bool isPublic)
        {
            BigInteger prime;
            if (isPublic)
            {
                prime = E;
            }
            else
            {
                prime = D;
            }

            int primeSize = prime.GetByteCount();
            int nSize = N.GetByteCount();

            Byte[] primeSizeBytes = BitConverter.GetBytes(primeSize);
            Byte[] nSizeBytes = BitConverter.GetBytes(nSize);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(primeSizeBytes);
                Array.Reverse(nSizeBytes);
            }

            Byte[] primeBytes = prime.ToByteArray();
            Byte[] nBytes = N.ToByteArray();

            Byte[] keyByteArray = new Byte[primeSizeBytes.Length + primeBytes.Length + nSizeBytes.Length + nBytes.Length];
            primeSizeBytes.CopyTo(keyByteArray, 0);
            primeBytes.CopyTo(keyByteArray, primeSizeBytes.Length);
            nSizeBytes.CopyTo(keyByteArray, primeSizeBytes.Length + primeBytes.Length);
            nBytes.CopyTo(keyByteArray, primeSizeBytes.Length + primeBytes.Length + nSizeBytes.Length);
            return keyByteArray;
        }

        /// <summary>
        /// The ModInverse function provided to us for this project
        /// </summary>
        /// <returns>The modular inverse of the numbers</returns>
        static BigInteger ModInverse(BigInteger a, BigInteger n)
        {
            BigInteger i = n, v = 0, d = 1;
            while (a > 0)
            {
                BigInteger t = i / a, x = a;
                a = i % x;
                i = x;
                x = d;
                d = v - t * x;
                v = x;
            }
            v %= n;
            if (v < 0) v = (v + n) % n;
            return v;
        }
    }

    /// <summary>
    /// This class handles the RSA encryption and key extraction
    /// </summary>
    class RSAClient
    {
        private BigInteger prime;
        private BigInteger N;

        /// <summary>
        /// Extract the prime (either E or D) and N from the base64 encoded string and store them in the class instance
        /// </summary>
        /// <param name="encodedKey">The base64 encoded key string</param>
        public RSAClient(string encodedKey)
        {
            var bytes = Convert.FromBase64String(encodedKey);

            //First 4 bytes represent size of X
            Byte[] sizePrimeBytes = bytes[0..4];

            //Make sure that the size is big endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(sizePrimeBytes);
            }
            BigInteger numPrimeBytes = new BigInteger(sizePrimeBytes);

            //Should drop first 4 bytes
            bytes = bytes[4..];

            Byte[] primeByteArray = bytes[0..((int)numPrimeBytes)]; // Should get all the bytes from X

            bytes = bytes[((int)numPrimeBytes)..]; // Should drop X bytes from array

            //First 4 bytes represent size of X
            Byte[] sizeNBytes = bytes[0..4];
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(sizeNBytes);
            }
            BigInteger numNBytes = new BigInteger(sizeNBytes);

            bytes = bytes[4..];
            //What's left in bytes should be N
            Byte[] NByteArray = bytes[..((int)numNBytes)];

            //Store in OBJ
            prime = new BigInteger(primeByteArray);
            N = new BigInteger(NByteArray);
        }

        /// <summary>
        /// Perform the RSA encryption or decryption using the ModPow function
        /// </summary>
        /// <param name="text">The plaintext to encrypt or ciphertext to decrypt</param>
        /// <returns>The result of text^prime mod N</returns>
        public BigInteger RSA(BigInteger text)
        {
            return BigInteger.ModPow(text, prime, N);
        }
    }

    /// <summary>
    /// Provides an easy way to store a message for JSON encoding purposes
    /// </summary>
    class Message
    {
        public string Email { get; set; }
        public string Content { get; set; }

        public Message(string email, string content)
        {
            Email = email;
            Content = content;
        }
    }

    /// <summary>
    /// A representation of a key used in RSA encryption
    /// </summary>
    class RSAKey
    {
        public string Key { get; set; }

        public RSAKey()
        {
            this.Key = "";
        }

        public RSAKey(string key)
        {
            this.Key = key;
        }
    }

    /// <summary>
    /// A subset of the RSAkey class representing a public key for RSA
    /// </summary>
    class PublicKey : RSAKey
    {
        public string Email { get; set; }

        public PublicKey(string email, string key)
        {
            this.Email = email;
            this.Key = key;
        }
    }

    /// <summary>
    /// A subset of the RSAkey class representing a private key for RSA
    /// </summary>
    class PrivateKey : RSAKey
    {
        public List<String> Emails { get; set; }

        public PrivateKey(List<String> emails, string key)
        {
            this.Emails = emails;
            this.Key = key;
        }

        public void AddEmail(String email)
        {
            Emails.Add(email);
        }
    }
}
using System;
using Test;

namespace Test32
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                MemoryModuleSXTest.Test();
            }
            catch (Exception)
            {
                Console.WriteLine("failed");
            }
            Console.WriteLine("successful");
        }
    }
}

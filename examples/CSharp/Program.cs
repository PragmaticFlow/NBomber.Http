using System;
using System.Diagnostics.Tracing;
using System.Text.Json;

namespace CSharp
{
    public class UserData
    {
        public int UserId { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public bool Completed { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            new SimpleExample().Run();
            // new SequentialSteps().Run();
        }
    }
}

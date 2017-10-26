using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quizful.net.Tests
{
    class Program
    {
        class A
        {
            public virtual void Print()
            {
                Console.WriteLine("A::Print");
            }
        }

        class B : A
        {
            public override void Print()
            {
                Console.WriteLine("B::Print");
            }
        }

        class C : B
        {
            public new void Print()
            {
                base.Print();

                Console.WriteLine("C::Print");
            }
        }

        static void Main(string[] args)
        {
            //proc1();
            proc2();

            Console.ReadKey();
        }

        static IEnumerable<char> GetLetters()
        {
            yield return 'A';
            yield break;
            yield return 'B';
            yield return 'C';
        }

        private static void proc2()
        {
            foreach (char ch in GetLetters())
            {
                Console.Write(ch);
            }
        }

        private static void proc1()
        {
            A a = new A();
            A b = new B();
            B c = new C();

            a.Print();
            b.Print();
            c.Print();

            C c1 = (C)c;
            c1.Print();
        }

    }
}

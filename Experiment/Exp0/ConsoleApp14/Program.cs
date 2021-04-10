using ClassLibrary2;
using System;
using System.Collections.Generic;

namespace ConsoleApp14
{
    class Program
    {
        static void Main(string[] args)
        {
            //var collection = new List<int> { 1, 2, 3, 4, 5 };
            //var i = 1.In(collection);
            //Console.WriteLine($"Hello World: {i}");

            //var collection = new List<int> { 1, 2, 3, 4, 5 };
            //var a = 1;
            //var i = a.In(collection);
            //Console.WriteLine($"Hello World: {i}");

            //var v = global::ClassLibrary2.MyEnum.MyValue0;

            //var v = ClassLibrary2.MyEnum.MyValue0;

            //var v = typeof(global:: ClassLibrary2.MyEnum);
            //Console.WriteLine($"Hello World: {v}");

            //Console.WriteLine($"Hello World: {(global::ClassLibrary2.MyEnum.MyValue0)}");

            //var a = MyClass.MyConst;

            //var a = nameof(IMyGeneric<object>.Get);

            //var a = nameof(global::ClassLibrary2.IMyGeneric<object>.Get);

            //var a = nameof(ClassLibrary1.IMyGeneric<object>.Get);

            var a = MyEnum.MyValue0.ToString();
        }
    }
}

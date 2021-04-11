using ClassLibrary2;
using System;
using System.Collections.Generic;

namespace ConsoleApp14
{
    class Program
    {
        static void Main(string[] args)
        {
            //var collection0 = new List<int> { 1, 2, 3, 4, 5 };
            //var i0 = 1.In(collection0);
            //Console.WriteLine($"Hello World: {i0}");

            //var collection1 = new List<int> { 1, 2, 3, 4, 5 };
            //var a = 1;
            //var i1 = a.In(collection1);
            //Console.WriteLine($"Hello World: {i1}");

            //var v0 = global::ClassLibrary2.MyEnum.MyValue0;

            //var v1 = ClassLibrary2.MyEnum.MyValue0;

            //var v2 = typeof(global::ClassLibrary2.MyEnum);

            //Console.WriteLine($"Hello World: {(global::ClassLibrary2.MyEnum.MyValue0)}");

            //var a0 = MyClass.MyConst;

            //var a1 = nameof(IMyGeneric<object>.Get);

            //var a2 = nameof(global::ClassLibrary2.IMyGeneric<object>.Get);

            //var a3 = nameof(ClassLibrary2.IMyGeneric<object>.Get);

            //var a4 = MyEnum.MyValue0.ToString();

            var n0 = nameof(MyClass);

            var n1 = nameof(MyClass.MyConst);

            var n2 = nameof(ClassLibrary2.MyClass.MyConst);

            var n3 = nameof(global::ClassLibrary2.MyClass.MyConst);
        }
    }
}

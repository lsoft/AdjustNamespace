using ClassLibrary2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp14
{
    public interface IInterfaceWithGenericMethod
    {
        void DoSomething0<T>()
            where T : global:: ClassLibrary2.ISettings;

        void DoSomething1<T>()
            where T : ClassLibrary2.ISettings;

        void DoSomething2<T>()
            where T : ISettings;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient.View
{
    public interface IJoinSortedCollection<T> : IContainIDField
    {
        int Index { get; set; }

        void FillDataFromServiceObject(T sourceObject, int index = 1);

        void UpdateFromSvc(T sourceObject);
    }

}

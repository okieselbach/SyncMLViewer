using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace SyncMLViewer
{
    internal class AutoCompleteModel
    {
        private List<string> _data = new List<string>();

        public AutoCompleteModel()
        {
            _data.Add("./DevDetail/OEM");
            _data.Add("./DevDetail/Ext/Microsoft/DeviceName");
            _data.Add("./DevDetail/Ext/Microsoft/HardwareData");
            _data.Add("./DevDetail/Ext/Microsoft/LocalTime");
            _data.Add("./DevDetail/Ext/Microsoft/OSPlatform");
            _data.Add("./DevDetail/Ext/Microsoft/Resolution");
            _data.Add("./DevDetail/Ext/Microsoft/SMBIOSSerialNumber");
            _data.Add("./DevDetail/Ext/Microsoft/SystemSKU");
            _data.Add("./DevDetail/Ext/Microsoft/TotalRAM");
            _data.Add("./DevDetail/Ext/Microsoft/TotalStorage");
            _data.Add("./Device/Vendor/MSFT/DMClient/Provider/MS%20DM%20SERVER/Recovery/AllowRecovery");
            _data.Add("./Device/Vendor/MSFT/LAPS/Policies");
        }

        public List<string> GetData()
        {
            return _data;
        }

        public void AddData(string data)
        {
            if (GetData().FindAll(x => x == data).Count > 0)
            {
                return;
            }
            else
            {
                _data.Add(data);
            }
        }

        public void ClearData()
        {
            _data.Clear();
        }
    }
}

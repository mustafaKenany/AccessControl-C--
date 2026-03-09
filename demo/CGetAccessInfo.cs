using FCardCDrive;
using FCardCDrive.Connect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Timers;
namespace demo
{
    public partial class CGetAccessInfo : Form
    {
        public CGetAccessInfo()
        {
            InitializeComponent();
        }
        public string ip;
        public string trgip;
        public string mac;
        public string sn;
        public string password;
        public string gateway;
        public int doorCount;
        public string tcpport;
        public string udpport;
        public string mode;
        public int monitor, accessCount;
        //Define the global command operation object
        public FCardCDrive.ConnectMain mIO = null;
        private void button1_Click(object sender, EventArgs e)
        {
            mIO.Command(GetConnectInfo(), "GetState");//Open door command

        }
        //Create connection parameter object
        private FCardCDrive.Connect.ConnectInfo GetConnectInfo()
        {
            FCardCDrive.Connect.ConnectInfo oInfo = new FCardCDrive.Connect.ConnectInfo();
            oInfo.SN = sn;// 16 characters
            oInfo.IP = ip;//IP
            oInfo.NetPort = 8000;//TCP default port is 8000
            oInfo.Password = password;//Communication password 8 hexadecimal characters
            oInfo.EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900;//Specify device type
            oInfo.ConnType = FCardCDrive.Connect.ConnectInfo.e_ConnectType.OnTCPClient;//Specify connection type
            oInfo.RestartCount = 3;//number of retries
            oInfo.TimeOutMSEL = 6000;//time out
            return oInfo;
        }
        private void CGetAccessInfo_Load(object sender, EventArgs e)
        {
            //Initialize command operation object
            if (mIO == null)
            {
                mIO = new FCardCDrive.ConnectMain();
                mIO.CommandAchieve += _CommandAchieve;//Command success event
                mIO.CommandTimeout += _CommandTimeout;//Command timeout event
                mIO.ConnectError += _ConnectError;//Connection error event
                mIO.PasswordError += _PasswordError;//Password error event
            }
        }

        public object GetObjValue(object obj, string PropertyName)
        {
            Type type = obj.GetType();
            object ret = type.GetProperty(PropertyName).GetValue(obj, null);
            if (ret.GetType().FullName == "System.UInt32")
                ret = (int)(uint)ret;
            return ret;
            object val = null;
            if (!ret.GetType().IsGenericType)
            {
                //Non-generic
                val = Convert.ChangeType(ret, ret.GetType());//PropertyType);
                //default()

            }
            else
            {
                Type genericTypeDefinition = ret.GetType().GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    val = Convert.ChangeType(ret, Nullable.GetUnderlyingType(ret.GetType()));
                }
            }
            return val;
        }
        private void _CommandAchieve(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            if (iCommandCode == (int)FCardCDrive.ConnectMain.eCommandCode.cmdOpenRelay)
            {
                //Use "iCommandCode" to determine which command belongs to the event to return
            }
            else if (iCommandCode == (int)FCardCDrive.ConnectMain.eCommandCode.cmdGetState)//Used to query the status of the controller
            {
                string magstatus="";
                byte[] DoorState = (byte[])GetObjValue(oValue, "DoorState");//Door status, 0 means closed, 1 means open
                byte[] DoorAlarmState = (byte[])GetObjValue(oValue, "DoorAlarmState");//Equipment alarm status
                byte[] DoorLongOpenState = (byte[])GetObjValue(oValue, "DoorLongOpenState");//Operating status
                byte[] LockState = (byte[])GetObjValue(oValue, "LockState");//Device lock status

                //DoorState represents the state of door sensor: here, the array output is parsed according to the number of doors of your own controller. Both the sdk and the bottom layer are based on 4 controllers.
                //That is to say, if you are a dual-door controller, the number of elements in the byte array is 2, and the single-door is 1, and you don’t need to care about the rest of the elements.
                magstatus = DoorState[0].ToString() + "\r\n" + DoorState[1].ToString() + ";" + DoorState[2].ToString() + ";" + DoorState[1].ToString()+ ";";
                MessageBox.Show(magstatus);
            }
        }
        private void _CommandTimeout(ConnectInfo oInfo, int iCommandCode, int iStep, object oValue)
        {
            //command time out
        }

        /// <summary>
        /// error in connecting
        /// </summary>
        /// <param name="oInfo">Return the connection parameter object when the command is executed intact</param>
        /// <param name="iCommandCode">Command code, one-to-one correspondence with enumerated objects</param>
        /// <param name="oValue">Command return value</param>
        private void _ConnectError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// wrong password
        /// </summary>
        /// <param name="oInfo">returns the connection parameter object when the command is executed intact</param>
        /// <param name="iCommandCode">Command code, one-to-one correspondence with enumerated objects</param>
        /// <param name="oValue">Command return value</param>
        private void _PasswordError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {

        }
    }
}

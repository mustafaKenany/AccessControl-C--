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
    public partial class DataMonitor
    {
        //Define the global command operation object
        public static FCardCDrive.ConnectMain mIO = null;

        public List<string> _listInfo = new List<string>();//Store the controller information to be monitored

        public System.Timers.Timer timer1;
        public void OpenMonitor()//Turn on monitoring
        {
            timer1 = new System.Timers.Timer(10000);
            timer1.Elapsed += new System.Timers.ElapsedEventHandler(theout);
            //Connect the event of the timer to the time and the method of handling the event through delegation, enable the timer, and send monitoring instructions regularly to prevent the device from dropping
 
            timer1.AutoReset = true;
            //Repeat timing
            timer1.Enabled = true;

            if (mIO == null)
            {
                mIO = new FCardCDrive.ConnectMain();
                mIO.CommandAchieve += _CommandAchieve;//Command success event
                mIO.CommandTimeout += _CommandTimeout;//Command timeout event
                mIO.ConnectError += _ConnectError;//Connection error event
                mIO.PasswordError += _PasswordError;//Password error event
                mIO.WatchEvent += _WatchEvent;
            }
            for (int i = 0; i < _listInfo.Count() / 4; i++)
            {
                FCardCDrive.Connect.ConnectInfo oInfo = new FCardCDrive.Connect.ConnectInfo();

                oInfo.SN = _listInfo[i * 4];// 16 characters
                oInfo.IP = _listInfo[i * 4+1];//IP
                oInfo.NetPort = 8000;//TCP default port is 8000
                oInfo.Password = _listInfo[i * 4+3];//Communication password 8 hexadecimal characters
                if (oInfo.SN.Contains("CR-3212T") || oInfo.SN.Contains("CR-3222T") || oInfo.SN.Contains("CR-3242T") || oInfo.SN.Contains("CR-3216H") || oInfo.SN.Contains("CR-3226H") || oInfo.SN.Contains("CR-3246H"))
                {
                    oInfo.EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900H;//Specify device type
                }
                else
                {
                    oInfo.EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900;//Specify device type
                }
                oInfo.ConnType = FCardCDrive.Connect.ConnectInfo.e_ConnectType.OnTCPClient;//Specify connection type
                oInfo.RestartCount = 3;//number of retries
                oInfo.TimeOutMSEL = 600;//time out

                mIO.Command(oInfo, "BeginWatch");
            }
        }
        public void CloseMonitor()//Turn off monitoring
        {
            timer1.Enabled = false;
            if (mIO == null)
            {
                mIO = new FCardCDrive.ConnectMain();
                mIO.CommandAchieve += _CommandAchieve;//Command success event
                mIO.CommandTimeout += _CommandTimeout;//Command timeout event
                mIO.ConnectError += _ConnectError;//Connection error event
                mIO.PasswordError += _PasswordError;//Password error event
                mIO.WatchEvent += _WatchEvent;
            }
            for (int i = 0; i < _listInfo.Count() / 4; i++)
            {
                FCardCDrive.Connect.ConnectInfo oInfo = new FCardCDrive.Connect.ConnectInfo();

                oInfo.SN = _listInfo[i * 4];// 16 characters
                oInfo.IP = _listInfo[i * 4 + 1];//IP
                oInfo.NetPort = 8000;//TCP default port is 8000
                oInfo.Password = _listInfo[i * 4 + 3];//Communication password 8 hexadecimal characters

                if (oInfo.SN.Contains("CR-3212T") || oInfo.SN.Contains("CR-3222T") || oInfo.SN.Contains("CR-3242T") || oInfo.SN.Contains("CR-3216H") || oInfo.SN.Contains("CR-3226H") || oInfo.SN.Contains("CR-3246H"))
                {
                    oInfo.EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900H;//Specify device type
                }
                else
                {
                    oInfo.EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900;//Specify device type
                }

                oInfo.ConnType = FCardCDrive.Connect.ConnectInfo.e_ConnectType.OnTCPClient;//Specify connection type
                oInfo.RestartCount = 3;//number of retries
                oInfo.TimeOutMSEL = 600;//time out

                mIO.Command(oInfo, "CloseWatch");
            }

        }
        public void theout(object sender, ElapsedEventArgs e)
        {
            if (mIO == null)
            {
                mIO = new FCardCDrive.ConnectMain();
                mIO.CommandAchieve += _CommandAchieve;//Command success event
                mIO.CommandTimeout += _CommandTimeout;//Command timeout event
                mIO.ConnectError += _ConnectError;//Connection error event
                mIO.PasswordError += _PasswordError;//Password error event
                mIO.WatchEvent += _WatchEvent;
            }
            for (int i = 0; i < _listInfo.Count() / 4; i++)
            {
                FCardCDrive.Connect.ConnectInfo oInfo = new FCardCDrive.Connect.ConnectInfo();

                oInfo.SN = _listInfo[i * 4];// 16 characters
                oInfo.IP = _listInfo[i * 4 + 1];//IP
                oInfo.NetPort = 8000;//TCP default port is 8000
                oInfo.Password = _listInfo[i * 4 + 3];//Communication password 8 hexadecimal characters
                oInfo.EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900;//Specify device type
                oInfo.ConnType = FCardCDrive.Connect.ConnectInfo.e_ConnectType.OnTCPClient;//Specify connection type
                oInfo.RestartCount = 3;//number of retries
                oInfo.TimeOutMSEL = 600;//time out

                mIO.Command(oInfo, "BeginWatch");
            }
        }

        #region Data monitoring
        //Event corresponding method
        /// <summary>
        /// Command success event
        /// </summary>
        /// <param name="oInfo">returns the connection parameter object when the command is executed intact</param>
        /// <param name="iCommandCode">Command code, one-to-one correspondence with enumerated objects</param>
        /// <param name="oValue">Command return value</param>
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
            if (iCommandCode == (int)FCardCDrive.ConnectMain.eCommandCode.cmdReadRecord)
            {
                //Non-sorted
                //Determine which command event belongs to by iCommandCode to return
            }
            else if (iCommandCode == (int)FCardCDrive.ConnectMain.eCommandCode.cmdGetState)//用于查询控制器的状态
            {
                string magstatus = "";
                byte[] DoorState = (byte[])GetObjValue(oValue, "DoorState");//Door status, 0 means closed, 1 means open
                byte[] DoorAlarmState = (byte[])GetObjValue(oValue, "DoorAlarmState");//Equipment alarm status
                byte[] DoorLongOpenState = (byte[])GetObjValue(oValue, "DoorLongOpenState");//Operating status
                byte[] LockState = (byte[])GetObjValue(oValue, "LockState");//Device lock status

                //DoorState represents the door sensor state: here, the array output is parsed according to the number of doors of your own controller. Both the sdk and the bottom layer are based on 4 controllers.
                //That is to say, if you are a dual-door controller, the number of elements in the byte array is 2, and the single-door is 1, and you don’t need to care about the rest of the elements.
                magstatus = DoorState[0].ToString() + ";" + DoorState[1].ToString() + ";" + DoorState[2].ToString() + ";" + DoorState[1].ToString() + ";";
                MessageBox.Show(magstatus);
            }
        }

        /// <summary>
        /// command time out
        /// </summary>
        /// <param name="oInfo">returns the connection parameter object when the command is executed intact</param>
        /// <param name="iCommandCode">Command code, one-to-one correspondence with enumerated objects</param
        /// <param name="iStep">the step where the error occurred</param>
        /// <param name="oValue">Command return value</param>
        private void _CommandTimeout(ConnectInfo oInfo, int iCommandCode, int iStep, object oValue)
        {
            //Command time out
        }

        /// <summary>
        /// error in connecting
        /// </summary>
        /// <param name="oInfo">returns the connection parameter object when the command is executed intact</param>
        /// <param name="iCommandCode">Command code, one-to-one correspondence with enumerated objects</param>
        /// <param name="oValue">Command return value</param>
        private void _ConnectError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// wrong password
        /// </summary>
        /// <param name="oInfo">returns the connection parameter object when the command is executed intact</param>
        /// <param name="iCommandCode">Command code, one-to-one correspondence with enumerated objects</param>
        /// <param name="oValue">Command return value</param>
        private void _PasswordError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            switch (iCommandCode)
            {
                case (int)FCardCDrive.ConnectMain.eCommandCode.cmdBeginWatch:
                    MessageBox.Show("[Open monitoring] Command processing failed due to communication password error!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
                case (int)FCardCDrive.ConnectMain.eCommandCode.cmdCloseWatch:

                    MessageBox.Show("[Close monitoring] The command processing failed due to a Communication password error!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Data monitoring
        /// </summary>
        /// <param name="oInfo">returns the connection parameter object when the command is executed intact</param>
        /// <param name="iRecordCode">Record code</param>
        /// <param name="bData">Data</param>
        private void _WatchEvent(FCardCDrive.Connect.ConnectInfo oInfo, int iRecordCode, byte[] bData)
        {
            Record oRecord = null;
            switch (iRecordCode)
            {
                case 0x23:
                    //Connection confirmation message
                    break;
                case 0x22:
                    //Connect keepalive (heartbeat keepalive)                    
                    break;
                case 0x24:
                    //Client is offline                   
                    break;
                case 0x25:
                    //Client is online
                    break;
                case 0xF0:

                    break;
                default:

                    if (oInfo.SN.Contains("CR-3212T") || oInfo.SN.Contains("CR-3222T") || oInfo.SN.Contains("CR-3242T") || oInfo.SN.Contains("CR-3216H") || oInfo.SN.Contains("CR-3226H") || oInfo.SN.Contains("CR-3246H"))
                    {

                        if (mIO.WatchRecordDecompile(ConnectInfo.e_EquptType.FC8900H, iRecordCode, (byte[])bData, ref oRecord))
                        {
                            string eventInfo;
                            string sDate = "";
                            if (!(oRecord.EventDate == System.DateTime.MinValue))
                            {
                                sDate += oRecord.EventDate.ToString("yyyy-MM-dd HH:mm:ss");
                            }

                            string inOut;//代表Come in还是Go out
                            if (oRecord.ReaderType == 1)
                            {
                                inOut = "Come in";
                            }
                            else
                            {
                                inOut = "Go out";
                            }
                            if (mRecordTypeName[(int)oRecord.RecordType] == "Card reading event")
                            {
                                eventInfo = string.Format("event:" + mRecordTypeName[(int)oRecord.RecordType] + ";SN:" + oInfo.SN + ";Card:" + oRecord.Card + ";Door number:" + oRecord.DoorNum + ";time:" + oRecord.EventDate + ";description:" + mEvnetCodeName[(int)oRecord.EventCode] + ";In and out state:" + inOut + "\r\n");
                            }
                            else
                            {
                                eventInfo = string.Format("event:" + mRecordTypeName[(int)oRecord.RecordType] + ";SN:" + oInfo.SN + ";Door number:" + oRecord.DoorNum + ";time:" + oRecord.EventDate + ";description:" + mEvnetCodeName[(int)oRecord.EventCode] + "\r\n");
                            }
                            //This process uploads the received information to the main form. If there is no form, ignore this step and output eventInfo directly, without defining OnInput
                            OnInput(this, new MyEventArg(eventInfo));
                        }
                    }
                    else
                    {
                        if (mIO.WatchRecordDecompile(ConnectInfo.e_EquptType.FC8900, iRecordCode, (byte[])bData, ref oRecord))
                        {
                            string eventInfo;
                            string sDate = "";
                            if (!(oRecord.EventDate == System.DateTime.MinValue))
                            {
                                sDate += oRecord.EventDate.ToString("yyyy-MM-dd HH:mm:ss");
                            }

                            string inOut;//代表Come in还是Go out
                            if (oRecord.ReaderType == 1)
                            {
                                inOut = "Come in";
                            }
                            else
                            {
                                inOut = "Go out";
                            }
                            if (mRecordTypeName[(int)oRecord.RecordType] == "Card reading event")
                            {
                                eventInfo = string.Format("event:" + mRecordTypeName[(int)oRecord.RecordType] + ";SN:" + oInfo.SN + ";Card:" + oRecord.Card + ";Door number:" + oRecord.DoorNum + ";time:" + oRecord.EventDate + ";description:" + mEvnetCodeName[(int)oRecord.EventCode] + ";In and out state:" + inOut + "\r\n");
                            }
                            else
                            {
                                eventInfo = string.Format("event:" + mRecordTypeName[(int)oRecord.RecordType] + ";SN:" + oInfo.SN + ";Door number:" + oRecord.DoorNum + ";time:" + oRecord.EventDate + ";description:" + mEvnetCodeName[(int)oRecord.EventCode] + "\r\n");
                            }
                            //This process uploads the received information to the main form. If there is no form, ignore this step and output eventInfo directly, without defining OnInput
                            OnInput(this, new MyEventArg(eventInfo));
                        }
                    }
                    break;
            }

        }

        #endregion

        #region Define event type
        public event EventHandler<MyEventArg> OnInput;//Define events for receiving data monitoring
        private string[] mRecordTypeName = new string[11];//Record type Meaning array collection
        private string[] mEvnetCodeName = new string[301];//Operation event type meaning Meaning array collection
        public void Init()//Initialization event type
        {
            ///Record type
            mRecordTypeName[(int)Record.eRecordType.eCardRecord] = "Card reading event";
            mRecordTypeName[(int)Record.eRecordType.eButtonRecord] = "Button record";
            mRecordTypeName[(int)Record.eRecordType.eDoorStateRecord] = "Door magnetic record";
            mRecordTypeName[(int)Record.eRecordType.eSoftwareRecord] = "Remote door opening record";
            mRecordTypeName[(int)Record.eRecordType.eAlarmRecord] = "Alarm record";
            mRecordTypeName[(int)Record.eRecordType.eSystemRecord] = "System record";
            ///Operation event type meaning
            //Card reading recordCard reading record
            mEvnetCodeName[(int)Record.eRecordEvent.eCardOpen] = "Open the door legally";
            mEvnetCodeName[(int)Record.eRecordEvent.ePasswordOpen] = "Password to open the door";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardAndPasswordOpen] = "Card plus password";
            mEvnetCodeName[(int)Record.eRecordEvent.eInputCardOpen] = "Manually enter card and password";
            mEvnetCodeName[(int)Record.eRecordEvent.eFirstCardOpen] = "FirstCard Open door";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardHoldOpen] = "Door always open-card reading";
            mEvnetCodeName[(int)Record.eRecordEvent.eMuchCardOpen] = "Doka Open door";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardRepeat] = "Read Card Repeat";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardExpire] = "Validity expires";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardTimeGroupExpire] = "Open door period expired";
            mEvnetCodeName[(int)Record.eRecordEvent.eHolidayInvalid] = "Holidays are invalid";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardInvalid] = "Illegal Card";
            mEvnetCodeName[(int)Record.eRecordEvent.ePatrolCard] = "Patrol Card";
            mEvnetCodeName[(int)Record.eRecordEvent.eProbeLocked] = "Probe Locked";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardOpenCountInvalid] = "Effective times have been used up";
            mEvnetCodeName[(int)Record.eRecordEvent.eSlipInto] = "Anti-slipback";
            mEvnetCodeName[(int)Record.eRecordEvent.ePasswordError] = "Password error";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardPasswordError] = "Password and Card-Password Error";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardLocked] = "Card reading is prohibited when locked";
            mEvnetCodeName[(int)Record.eRecordEvent.eLockedPassword] = "Password forbidden when locked";
            mEvnetCodeName[(int)Record.eRecordEvent.eFirstCardInvalid] = "The first card is not opened";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardLose] = "Report Lost Card";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardBlacklist] = "Blacklist Card";
            mEvnetCodeName[(int)Record.eRecordEvent.ePeopleFull] = "The upper limit in the door is full, and Come in is prohibited";
            mEvnetCodeName[(int)Record.eRecordEvent.eOpenAntiTheft_Card] = "Open the anti-theft host (setting card)";
            mEvnetCodeName[(int)Record.eRecordEvent.eCloseAntiTheft_Card] = "Close the anti-theft host (setting card)";
            mEvnetCodeName[(int)Record.eRecordEvent.eOpenAntiTheft_PWD] = "Open AntiTheft Host (password)";
            mEvnetCodeName[(int)Record.eRecordEvent.eCloseAntiTheft_PWD] = "Close AntiTheft Host (password)";
            mEvnetCodeName[(int)Record.eRecordEvent.eInterlockCardInvalid] = "Card reading is prohibited during interlock";
            mEvnetCodeName[(int)Record.eRecordEvent.eInterlockPasswordInvalid] = "Password is prohibited during interlock";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardRegFree] = "Open the door with full card";
            mEvnetCodeName[(int)Record.eRecordEvent.eMuchCardWait] = "Multi-Card-Waiting for the next card";
            mEvnetCodeName[(int)Record.eRecordEvent.eMuchCardError] = "Multi-Card--Combination Error";
            mEvnetCodeName[(int)Record.eRecordEvent.eMuchCard] = "Card reading is prohibited during non-first card period";
            mEvnetCodeName[(int)Record.eRecordEvent.eFirstCardTimeInvalid] = "Password forbidden for non-first card time period";
            mEvnetCodeName[(int)Record.eRecordEvent.eForbidCard] = "Card reader is forbidden to open the door";
            mEvnetCodeName[(int)Record.eRecordEvent.eForbidPassword] = "Password opening is forbidden";
            mEvnetCodeName[(int)Record.eRecordEvent.eWaitEnterRequest] = "The card has been read inside the door, waiting for the card to be read outside the door";
            mEvnetCodeName[(int)Record.eRecordEvent.eWaitExitRequest] = "The card has been read outside the door, waiting for the card to be read inside the door";
            mEvnetCodeName[(int)Record.eRecordEvent.eWaitManager] = "The normal card has been read, please read the management card";
            mEvnetCodeName[(int)Record.eRecordEvent.eWaitCustomer] = "You have read the management card, please read the normal card";
            mEvnetCodeName[(int)Record.eRecordEvent.eFirstCardPasswordInvalid] = "Password prohibited during the first card period";
            mEvnetCodeName[(int)Record.eRecordEvent.eControlInvalid_Card] = "The controller has expired_read card";
            mEvnetCodeName[(int)Record.eRecordEvent.eControlInvalid_PWD] = "The controller has expired_password";
            mEvnetCodeName[(int)Record.eRecordEvent.eCardImminentInvalid] = "Legal opening-the validity period is about to expire";
            mEvnetCodeName[(int)Record.eRecordEvent.eAreaCheckRepeatInoutOffline] = "Area CheckRepeatInoutOffline";
            mEvnetCodeName[(int)Record.eRecordEvent.eArealinkageLockOffline] = "Area Interlock-Access Denied (Loss Connection with Server)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAreaCheckRepeatInout] = "Area CheckRepeatInout";
            mEvnetCodeName[(int)Record.eRecordEvent.eArealinkageLock] = "Area interlocking-if the door is not closed properly, refuse to open the door";




            //button event
            mEvnetCodeName[(int)Record.eRecordEvent.eButton] = "Button to open the door";
            mEvnetCodeName[(int)Record.eRecordEvent.eButtonTimeInvalid] = "Open door period expired";
            mEvnetCodeName[(int)Record.eRecordEvent.eButtonLocked] = "Button Locked";
            mEvnetCodeName[(int)Record.eRecordEvent.eButtonControlExpire] = "The controller has expired";
            mEvnetCodeName[(int)Record.eRecordEvent.eButtonInvalid_Interlock] = "Button is prohibited during interlock";





            //Door magnetic recordDoor magnetic record
            mEvnetCodeName[(int)Record.eRecordEvent.eOpenedState] = "Open door";
            mEvnetCodeName[(int)Record.eRecordEvent.eClosedState] = "Close door";
            mEvnetCodeName[(int)Record.eRecordEvent.eOpenAntiLockPicking] = "Enter Door sensor alarmArmed state";
            mEvnetCodeName[(int)Record.eRecordEvent.eCloseAntiLockPicking] = "ExitDoor sensor alarmArmed state";
            mEvnetCodeName[(int)Record.eRecordEvent.eNoClosed] = "The door is not closed";



            //Software Operation Record Software Operation Record
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareOpen] = "Software open door";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareClose] = "Software open door";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareHoldOpen] = "Software Hold";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareAutoHoldOpen] = "The controller automatically enters normally open";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareAutoClosed] = "The controller automatically closes the door";
            mEvnetCodeName[(int)Record.eRecordEvent.eButtonHoldOpen] = "Long press the Go out button to always open";
            mEvnetCodeName[(int)Record.eRecordEvent.eButtonHoldClose] = "Long press the Go out button to normally close";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareLocked] = "Software Locked";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareUnlocked] = "Software Unlocked";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareAutoLocked] = "Controller timing lock";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareAutoUnlocked] = "The controller is unlocked regularly";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmLocked] = "Alarm-Locked";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmUnlocked] = "Alarm--Unlock";
            mEvnetCodeName[(int)Record.eRecordEvent.eSoftwareOpenInvalid_Interlock] = "Do not open the door remotely during interlock";


            //Alarm recordAlarm record
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmLockPicking] = "Door sensor alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmLockPickingClose] = "Door sensor alarm cancellation";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmLockPickingSoftwortClose] = "Door sensor alarm cancellation (remote close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmBandit] = "Police call the police";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmBanditClose] = "Police call the police revoked";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmBanditSoftwortClose] = "Police call the police cancellation (remote close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmFire] = "Fire alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmFireClose] = "Fire alarm cancellation";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmFireSoftwortClose] = "Fire alarm cancellation (remote close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmInvalidCard] = "Illegal card swipe alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmInvalidCardClose] = "Software illegal card swipe alarm cancellation";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmInvalidCardSoftwortClose] = "Illegal card swipe alarm cancellation (software close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmKidnap] = "Duress Alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmKidnapClose] = "Duress alarm cancellation";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmKidnapSoftwortClose] = "Duress alarm cancellation (remote close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmFireSoftwort] = "Fire alarm (remote shutdown)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmSmog] = "Smoke Alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmSmogClose] = "Smog alarm cancellation";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmSmogSoftwortClose] = "Smog alarm cancellation (remote close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmTheft] = "Burglar Alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmTheftClose] = "Anti-theft alarm cancellation";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmTheftSoftwortClose] = "Anti-theft alarm cancellation (remote close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmBalcklist] = "Blacklist Alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmBalcklistClose] = "Close the blacklist alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmBalcklistSoftwortClose] = "Close the blacklist alarm (software close)";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmNoClosed] = "Open door overtime alarm";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmNoClosedClose] = "Door open timeout alarm cancellation";
            mEvnetCodeName[(int)Record.eRecordEvent.eAlarmNoClosedSoftwortClose] = "Door open timeout alarm cancellation (remote close)";


            //System recordSystem record
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemRun] = "System is powered on";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemRestart] = "System error reset";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemFormat] = "Device Format Record";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemTemperatureHigh] = "System high temperature record, temperature is greater than >75";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemUPS] = "System UPS power supply record";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemTemperatureFault] = "The temperature sensor is damaged and the temperature is greater than >100";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemVoltageLow] = "The voltage is too low, less than <09V";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemVHigh] = "The voltage is too high, greater than >14V";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemReaderLineReverse] = "The reader is connected reversely";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemReaderLineError] = "The reader line is not connected properly";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemReaderInvalid] = "Unrecognized card reader";
            mEvnetCodeName[(int)Record.eRecordEvent.eSystemVRecover] = "The voltage returns to normal, less than 14V, greater than 9V";
            mEvnetCodeName[(int)Record.eRecordEvent.eLANDiscon] = "The network cable has been disconnected";
            mEvnetCodeName[(int)Record.eRecordEvent.eLANRecover] = "The network cable is connected";
        }
    }
    #endregion
    public class MyEventArg : EventArgs
    {
        string _msg;
        public MyEventArg (string msg)
        {
            this._msg = msg;
        }
        public string Msg
        {
            get { return String.IsNullOrEmpty(_msg) ? "Not String!" : _msg; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace demo
{
    public partial class CPoliceAlarm : Form
    {
        public CPoliceAlarm()
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
        public string openPassword;
        public string mode;

        private const int WM_COPYDATA = 0x004A;  //Message type 
        private const int WM_READ_ALARM_STATIC=100;  //Message type 
        private void CPoliceAlarm_Load(object sender, EventArgs e)
        {
            comboBox1.Items.Add("Off");
            comboBox1.Items.Add("Alarm and lock the door");
            comboBox1.Items.Add("Alarm does not lock the door");
            comboBox1.Items.Add("Signal alarm, no signal release");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if (index == -1)
            {
                MessageBox.Show("Please choose the alarm mode");
            }
            else
            {
                string[] s = { mac, ip, tcpport, sn, password, "0", "2", Convert.ToString(index), "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                IntPtr result = cl2.policeOfficer(s, 1);
            }
        }


        private void button3_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "0", "2", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 3);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "0", "2", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string alarmStatus = "";
            alarmStatus = cl2.fireAlarm(s, alarmStatus);
            textBox1.Text = alarmStatus;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

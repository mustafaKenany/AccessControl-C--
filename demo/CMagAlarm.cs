using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace demo
{
    public partial class CMagAlarm : Form
    {
        public CMagAlarm()
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
        public string[] openTime = new string[7];//定义Open door时段
        private void CMagAlarm_Load(object sender, EventArgs e)
        {
            comboBox2.Items.Add("Monday");
            comboBox2.Items.Add("Tuesday");
            comboBox2.Items.Add("Wednesday");
            comboBox2.Items.Add("Thursday");
            comboBox2.Items.Add("Friday");
            comboBox2.Items.Add("Saturday");
            comboBox2.Items.Add("Sunday");
            openTime[0] = "0000235900000000000000000000000000000000000000000000000000000000";
            openTime[1] = "0000235900000000000000000000000000000000000000000000000000000000";
            openTime[2] = "0000235900000000000000000000000000000000000000000000000000000000";
            openTime[3] = "0000235900000000000000000000000000000000000000000000000000000000";
            openTime[4] = "0000235900000000000000000000000000000000000000000000000000000000";
            openTime[5] = "0000235900000000000000000000000000000000000000000000000000000000";
            openTime[6] = "0000235900000000000000000000000000000000000000000000000000000000";
            startTime1.CustomFormat = "HH:mm";
            startTime2.CustomFormat = "HH:mm";
            startTime3.CustomFormat = "HH:mm";
            startTime4.CustomFormat = "HH:mm";
            startTime5.CustomFormat = "HH:mm";
            startTime6.CustomFormat = "HH:mm";
            startTime7.CustomFormat = "HH:mm";
            startTime8.CustomFormat = "HH:mm";
            endtime1.CustomFormat = "HH:mm";
            endtime2.CustomFormat = "HH:mm";
            endtime3.CustomFormat = "HH:mm";
            endtime4.CustomFormat = "HH:mm";
            endtime5.CustomFormat = "HH:mm";
            endtime6.CustomFormat = "HH:mm";
            endtime7.CustomFormat = "HH:mm";
            endtime8.CustomFormat = "HH:mm";

            if (doorCount == 1)
            {
                button1.Enabled = true;
                button3.Enabled = true;
                button4.Enabled = false;
                button5.Enabled = false;
                button6.Enabled = false;
                button7.Enabled = false;
                button8.Enabled = false;
                button9.Enabled = false;
            }
            else if (doorCount == 2)
            {
                button1.Enabled = true;
                button3.Enabled = true;
                button4.Enabled = true;
                button5.Enabled = true;
                button6.Enabled = false;
                button7.Enabled = false;
                button8.Enabled = false;
                button9.Enabled = false;
            }
            else
            {
                button1.Enabled = true;
                button3.Enabled = true;
                button4.Enabled = true;
                button5.Enabled = true;
                button6.Enabled = true;
                button7.Enabled = true;
                button8.Enabled = true;
                button9.Enabled = true;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string timeRange;
            //timeRange = startTime1.Text + startTime2.Text + startTime3.Text + startTime4.Text + startTime5.Text + startTime6.Text + startTime7.Text + startTime8.Text+ endtime1.Text + endtime2.Text + endtime3.Text + endtime4.Text + endtime5.Text + endtime6.Text + endtime7.Text + endtime7.Text;
            timeRange = startTime1.Text + endtime1.Text + startTime2.Text + endtime2.Text + startTime3.Text + endtime3.Text + startTime4.Text + endtime4.Text + startTime5.Text + endtime5.Text + startTime6.Text + endtime6.Text + startTime7.Text + endtime7.Text + startTime8.Text + endtime8.Text;
            timeRange = timeRange.Replace(":", "");
            if (comboBox2.Text == "Monday")
            {
                openTime[0] = timeRange;
            }
            else if (comboBox2.Text == "Tuesday")
            {
                openTime[1] = timeRange;
            }
            else if (comboBox2.Text == "Wednesday")
            {
                openTime[2] = timeRange;
            }
            else if (comboBox2.Text == "Thursday")
            {
                openTime[3] = timeRange;
            }
            else if (comboBox2.Text == "Friday")
            {
                openTime[4] = timeRange;
            }
            else if (comboBox2.Text == "Saturday")
            {
                openTime[5] = timeRange;
            }
            else if (comboBox2.Text == "Sunday")
            {
                openTime[6] = timeRange;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string timePieces = openTime[0] + openTime[1] + openTime[2] + openTime[3] + openTime[4] + openTime[5]+openTime[6];
            string[] s = { mac, ip, tcpport, sn, password, "1", "8", "1", "", "", "", "", "", "", "", timePieces, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.policeOfficer(s, 1);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string timePieces = openTime[0] + openTime[1] + openTime[2] + openTime[3] + openTime[4] + openTime[5] + openTime[6];
            string[] s = { mac, ip, tcpport, sn, password, "2", "8", "1", "", "", "", "", "", "", "", timePieces, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.policeOfficer(s, 1);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            string timePieces = openTime[0] + openTime[1] + openTime[2] + openTime[3] + openTime[4] + openTime[5] + openTime[6];
            string[] s = { mac, ip, tcpport, sn, password, "3", "8", "1", "", "", "", "", "", "", "", timePieces, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.policeOfficer(s, 1);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            string timePieces = openTime[0] + openTime[1] + openTime[2] + openTime[3] + openTime[4] + openTime[5] + openTime[6];
            string[] s = { mac, ip, tcpport, sn, password, "4", "8", "1", "", "", "", "", "", "", "", timePieces, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.policeOfficer(s, 1);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "0", "8", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 3);
        }
    }
}

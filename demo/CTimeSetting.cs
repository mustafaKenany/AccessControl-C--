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
    public partial class CTimeSetting : Form
    {
        public CTimeSetting()
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
        public string[] oftenTime = new string[4];//定义常开时段
        public string[] openTime = new string[7];//定义Open door时段
        private void CTimeSetting_Load(object sender, EventArgs e)
        {
            for (int i = 1; i < 65;i++ )
            {
                comboBox1.Items.Add("Open door时段"+Convert.ToString(i));
            }
            comboBox2.Items.Add("Monday");
            comboBox2.Items.Add("Tuesday");
            comboBox2.Items.Add("Wednesday");
            comboBox2.Items.Add("Thursday");
            comboBox2.Items.Add("Friday");
            comboBox2.Items.Add("Saturday");
            comboBox2.Items.Add("Sunday");
            comboBox3.Items.Add("The registration card is within the time period");
            comboBox3.Items.Add("Normally open card in time period");
            comboBox3.Items.Add("Set timing to enable according to time period");
            openTime[0] = "0000000000000000000000000000000000000000000000000000000000000000";
            openTime[1] = "0000000000000000000000000000000000000000000000000000000000000000";
            openTime[2] = "0000000000000000000000000000000000000000000000000000000000000000";
            openTime[3] = "0000000000000000000000000000000000000000000000000000000000000000";
            openTime[4] = "0000000000000000000000000000000000000000000000000000000000000000";
            openTime[5] = "0000000000000000000000000000000000000000000000000000000000000000";
            openTime[6] = "0000000000000000000000000000000000000000000000000000000000000000";
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

            if (this.Text == "Opening hours setting")
            {
                checkBox1.Visible = false;
                checkBox2.Visible = false;
                checkBox3.Visible = false;
                checkBox4.Visible = false;
                checkBox5.Visible = false;
                label19.Visible = false;
                label20.Visible = false;
                comboBox3.Visible = false;
                label1.Visible = true;
                comboBox1.Visible = true;
            }
            else if (this.Text == "Timing normally open setting")
            {
                checkBox1.Visible = true;
                checkBox2.Visible = true;
                checkBox3.Visible = true;
                checkBox4.Visible = true;
                checkBox5.Visible = true;
                label19.Visible = true;
                label20.Visible = true;
                comboBox3.Visible = true;
                label1.Visible = false;
                comboBox1.Visible = false;
                if (doorCount == 1)
                {
                    checkBox2.Enabled = true;
                    checkBox3.Enabled = false;
                    checkBox4.Enabled = false;
                    checkBox5.Enabled = false;
                }
                else if (doorCount == 2)
                {
                    checkBox2.Enabled = true;
                    checkBox3.Enabled = true;
                    checkBox4.Enabled = false;
                    checkBox5.Enabled = false;
                }
                else
                {
                    checkBox2.Enabled = true;
                    checkBox3.Enabled = true;
                    checkBox4.Enabled = true;
                    checkBox5.Enabled = true;
                }
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
            if(this.Text=="Opening hours setting")
            {
                int index = comboBox1.SelectedIndex + 1;
                string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                string timePieces = openTime[0] + openTime[1] + openTime[2] + openTime[3] + openTime[4] + openTime[5]+openTime[6] ;
                IntPtr result = cl2.setTimes(s, 3, index, timePieces);
            }
            else if (this.Text == "Timing normally open setting")
            {
                string[] doorCheck = { "1", "2", "3", "4" };
                string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                string timePieces = openTime[0] + openTime[1] + openTime[2] + openTime[3] + openTime[4] + openTime[5] + openTime[6];
                if (checkBox2.Checked)
                {
                    string temp="";
                    int index = comboBox3.SelectedIndex + 1;
                    if (checkBox1.Checked)
                    {
                        temp = temp + "01" + "040" + Convert.ToString(index) + "00" + timePieces;//01040300
                    }
                    else
                    {
                        temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    }
                    oftenTime[0] = temp;
                    doorCheck[0] = "1";
                }
                else 
                {
                    string temp = "";
                    int index = comboBox3.SelectedIndex + 1;
                    temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    oftenTime[0] = temp;
                    doorCheck[0] = "0";
                }
                if (checkBox3.Checked)
                {
                    string temp = "";
                    int index = comboBox3.SelectedIndex + 1;
                    if (checkBox1.Checked)
                    {
                        temp = temp + "01" + "040" + Convert.ToString(index) + "00" + timePieces;
                    }
                    else
                    {
                        temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    }
                    oftenTime[1] = temp;
                    doorCheck[1] = "2";
                }
                else
                {
                    string temp = "";
                    int index = comboBox3.SelectedIndex + 1;
                    temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    oftenTime[1] = temp;
                    doorCheck[1] = "0";
                }
                if (checkBox4.Checked)
                {
                    string temp = "";
                    int index = comboBox3.SelectedIndex + 1;
                    if (checkBox1.Checked)
                    {
                        temp = temp + "01" + "040" + Convert.ToString(index) + "00" + timePieces;
                    }
                    else
                    {
                        temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    }
                    oftenTime[2] = temp;
                    doorCheck[2] = "3";
                }
                else
                {
                    string temp = "";
                    int index = comboBox3.SelectedIndex + 1;
                    temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    oftenTime[2] = temp;
                    doorCheck[2] = "0";
                }
                if (checkBox5.Checked)
                {
                    string temp = "";
                    int index = comboBox3.SelectedIndex + 1;
                    if (checkBox1.Checked)
                    {
                        temp = temp + "01" + "040" + Convert.ToString(index) + "00" + timePieces;
                    }
                    else
                    {
                        temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    }
                    oftenTime[3] = temp;
                    doorCheck[3] = "4";
                }
                else
                {
                    string temp = "04";
                    int index = comboBox3.SelectedIndex + 1;
                    temp = temp + "00" + "040" + Convert.ToString(index) + "00" + timePieces;
                    oftenTime[3] = temp;
                    doorCheck[3] = "0";
                }
                IntPtr result = cl2.regularTime(s, 1, doorCheck, oftenTime);
                //string[] s2 = { "0-18-6-12-34-ed ", "192.168.12.152", tcpport, "CA-3240T48010097", password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                //IntPtr result2 = cl2.regularTime(s2, 1, doorCheck, oftenTime);
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

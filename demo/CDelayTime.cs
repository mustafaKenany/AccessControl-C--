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
    public partial class CDelayTime : Form
    {
        public CDelayTime()
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
        private void CDelayTime_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string delayTime=textBox1.Text;
            string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            string[] doorCheck = { "1","2","3","4"};

            if (delayTime.Length > 5)
            {
                MessageBox.Show("The length of time exceeds 5 digits");
            }
            else
            {
                if(checkBox1.Checked)
                {
                    doorCheck[0] = "1";
                }
                else
                {
                    doorCheck[0] = "0";
                }
                if(checkBox2.Checked)
                {
                    doorCheck[1] = "2";
                }
                else
                {
                    doorCheck[1] = "0";
                }
                if(checkBox3.Checked)
                {
                    doorCheck[2] = "3";
                }
                else
                {
                    doorCheck[2] = "0";
                }
                if(checkBox4.Checked)
                {
                    doorCheck[3] = "4";
                }
                else
                {
                    doorCheck[3] = "0";
                }
                if (delayTime.Length < 4)
                {
                    delayTime=delayTime.ToString().PadLeft(4, '0');
                }
                IntPtr result = cl2.openTimeDelay(s, 3, doorCheck, delayTime);
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!(Char.IsNumber(e.KeyChar)) && e.KeyChar != (char)8)
            {
                e.Handled = true;
                MessageBox.Show("Please key in numbers!");
            }
            else
            {
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

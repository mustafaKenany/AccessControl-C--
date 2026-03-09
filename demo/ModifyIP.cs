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
    public partial class ModifyIP : Form
    {
        public ModifyIP()
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
        public int a = 0;
        private void button1_Click(object sender, EventArgs e)
        {
            mac = textBox1.Text;
            trgip = textBox3.Text;
            gateway = textBox4.Text;
            tcpport = textBox7.Text;
            udpport = textBox8.Text;
            mode = comboBox1.Text;
            string[] s = { mac, trgip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.updateIP(s);
            a = 1;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close(); 
        }

        private void ModifyIP_Load(object sender, EventArgs e)
        {

        }
    }
}

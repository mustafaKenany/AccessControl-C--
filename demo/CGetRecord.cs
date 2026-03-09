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
    public partial class CGetRecord : Form
    {
        public CGetRecord()
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
        int recordType=1;
        string[] cardRecord = new string[200000];
        string[] buttonRecord = new string[200000];
        string[] magRecord = new string[200000];
        string[] remoteRecord = new string[200000];
        string[] alarmRecord = new string[200000];
        string[] systemRecord = new string[200000];

        public int port;//server port
        private void CGetRecord_Load(object sender, EventArgs e)
        {

        }
        const int SEND_PATH = 0x004A;

        private const int WM_GET_ALL_RECORD = 200;  //Message type 

        private void button2_Click(object sender, EventArgs e)
        {
            recordType = 1;
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport };
            
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string formtext = this.Text;
            //IntPtr result = cl2.getRecord(sn, ip, Convert.ToInt16(tcpport), password, recordType, outvalue, true, formtext, 0);
            //View after extract
            int textRecord=0;
            textRecord = cl2.getRecord(sn, ip, Convert.ToInt16(tcpport), password, recordType);
            if (textRecord==1)
            {
                MessageBox.Show("Successful collection");
            }
            else
            {
                MessageBox.Show("Acquisition failed");
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            recordType = 0;
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport };
            
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string formtext = this.Text;
            //View records
            int textRecord=0;

            textRecord = cl2.getRecord(sn, ip, Convert.ToInt16(tcpport), password, recordType);
            if (textRecord == 1)
            {
                MessageBox.Show("Successful collection");
            }
            else
            {
                MessageBox.Show("Acquisition failed");
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            recordType = 2;
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport };
            
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string formtext = this.Text;
            //View records
            int textRecord=0;

            textRecord = cl2.getRecord(sn, ip, Convert.ToInt16(tcpport), password, recordType);
            if (textRecord == 1)
            {
                MessageBox.Show("Successful collection");
            }
            else
            {
                MessageBox.Show("Acquisition failed");
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            recordType = 3;
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport };
            
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string formtext = this.Text;
            //View records
            int textRecord=0;

            textRecord = cl2.getRecord(sn, ip, Convert.ToInt16(tcpport), password, recordType);
            if (textRecord == 1)
            {
                MessageBox.Show("Successful collection");
            }
            else
            {
                MessageBox.Show("Acquisition failed");
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            recordType = 5;
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport };
            
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string formtext = this.Text;
            //View records
            int textRecord=0 ;

            textRecord = cl2.getRecord(sn, ip, Convert.ToInt16(tcpport), password, recordType);
            if (textRecord == 1)
            {
                MessageBox.Show("Successful collection");
            }
            else
            {
                MessageBox.Show("Acquisition failed");
            }
        }

        private void button12_Click(object sender, EventArgs e)
        {
            recordType = 4;
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport };
            
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string formtext = this.Text;
            //View records
            int textRecord=0;

            textRecord = cl2.getRecord(sn, ip, Convert.ToInt16(tcpport), password, recordType);
            if (textRecord == 1)
            {
                MessageBox.Show("Successful collection");
            }
            else
            {
                MessageBox.Show("Acquisition failed");
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
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

        private void button3_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Application.StartupPath + "\\RecordType.xls");  
        }

    }
}

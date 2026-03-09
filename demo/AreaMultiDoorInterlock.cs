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
    public partial class AreaMultiDoorInterlock : Form
    {
        public AreaMultiDoorInterlock()
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
        private void button1_Click(object sender, EventArgs e)
        {
            /*Here are examples of CR-3242T49100329 four-door controller and CR-3212T19120027 single-door controller,
              The incoming content of the AreaInterlock interface includes: controller information, whether to enable cross-area interlock, 0 for Disable, 1 for Enable, controller type (master: 1, slave 0), door port number, host IP,
              * The number of the controller in the entire area. Generally speaking, there is only one master, so the master is defined as 0, and the number of the slave is deduced by analogy. For example, set 3212T19120027 as the slave,
              * Then num is defined as 1, if there is more than one slave, num will increase sequentially
              */

            //string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            string[] s = { "00-18-06-14-B2-32", "192.168.12.156", "8000", "CR-3242T49100329", "ffffffff", null, null, null, null, null, null, null, null, null, null, null, "00-18-06-14-B2-32", "255.255.255.0", "192.168.12.1", "0.0.0.0", "0.0.0.0", "server", "8000", "8101", "0.0.0.0", "", "9010", null, null, "4" };
            int ww = cl2.AreaInterlock(s, 1, 1, 1,"CR-3242T49100329", "192.168.12.156",0);

            string[] s2 = { "00-18-06-14-B2-32", "192.168.12.156", "8000", "CR-3242T49100329", "ffffffff", null, null, null, null, null, null, null, null, null, null, null, "00-18-06-14-B2-32", "255.255.255.0", "192.168.12.1", "0.0.0.0", "0.0.0.0", "server", "8000", "8101", "0.0.0.0", "", "9010", null, null, "4" };
            ww = cl2.AreaInterlock(s2, 1, 1, 2,  "CR-3242T49100329", "192.168.12.156", 0);

            string[] s3 = { "00-18-06-14-B2-56", "192.168.12.157", "8000", "CR-3212T19120027", "ffffffff", null, null, null, null, null, null, null, null, null, null, null, "00-18-06-14-B2-56", "255.255.255.0", "192.168.12.1", "0.0.0.0", "0.0.0.0", "server", "8000", "8101", "0.0.0.0", "", "9010", null, null, "1" };
            ww = cl2.AreaInterlock(s3, 1, 0, 1,  "CR-3242T49100329", "192.168.12.156", 1);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            /*Here are examples of CR-3242T49100329 four-door controller and CR-3212T19120027 single-door controller,
              The incoming content of the AreaInterlock interface includes: controller information, whether to enable cross-area interlock, 0 for Disable, 1 for Enable, controller type (master: 1, slave 0), door port number, host IP,
              * The number of the controller in the entire area. Generally speaking, there is only one master, so the master is defined as 0, and the number of the slave is deduced by analogy. For example, set 3212T19120027 as the slave,
              * Then num is defined as 1. If there is more than one slave, num will increase in turn. It should be noted here that when Disable, the host IP can not be written
              */

            //string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            string[] s = { "00-18-06-14-B2-32", "192.168.12.156", "8000", "CR-3242T49100329", "ffffffff", null, null, null, null, null, null, null, null, null, null, null, "00-18-06-14-B2-32", "255.255.255.0", "192.168.12.1", "0.0.0.0", "0.0.0.0", "server", "8000", "8101", "0.0.0.0", "", "9010", null, null, "4" };
            int ww = cl2.AreaInterlock(s, 0, 1, 1, "CR-3242T49100329", "192.168.12.156", 0);

            string[] s2 = { "00-18-06-14-B2-32", "192.168.12.156", "8000", "CR-3242T49100329", "ffffffff", null, null, null, null, null, null, null, null, null, null, null, "00-18-06-14-B2-32", "255.255.255.0", "192.168.12.1", "0.0.0.0", "0.0.0.0", "server", "8000", "8101", "0.0.0.0", "", "9010", null, null, "4" };
            ww = cl2.AreaInterlock(s2, 0, 1, 2, "CR-3242T49100329", "192.168.12.156", 0);

            string[] s3 = { "00-18-06-14-B2-56", "192.168.12.157", "8000", "CR-3212T19120027", "ffffffff", null, null, null, null, null, null, null, null, null, null, null, "00-18-06-14-B2-56", "255.255.255.0", "192.168.12.1", "0.0.0.0", "0.0.0.0", "server", "8000", "8101", "0.0.0.0", "", "9010", null, null, "1" };
            ww = cl2.AreaInterlock(s3, 0, 0, 1, "CR-3242T49100329", "192.168.12.156", 1);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Management;
using System.Net.NetworkInformation;

namespace OpenCM_9_04_GUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] portnames = SerialPort.GetPortNames();
            comboBox1.Items.AddRange(portnames);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.PortName = comboBox1.Text;
                serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);

                serialPort1.Open();

                comboBox1.Enabled = false;
                comboBox2.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = true;
                button3.Enabled = true;
                button6.Enabled = true;
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;

                textBox1.Text += "Port " + serialPort1.PortName.ToString() + " is open" + Environment.NewLine;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.Close();

                button1.Enabled = true;
                comboBox1.Enabled = true;
                comboBox2.Enabled = true;
                button2.Enabled = false;
                button3.Enabled = false;
                button6.Enabled = false;
                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;
                numericUpDown3.Enabled = false;
                numericUpDown1.Value = 0;
                numericUpDown2.Value = 0;
                numericUpDown3.Value = 0;

                textBox1.Text += "Port " + serialPort1.PortName.ToString() + " is closed" + Environment.NewLine;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            byte[] buffer = new byte[7];
            buffer[0] = (byte)1;
            byte[] joint1_arr = new byte[2];
            byte[] joint2_arr = new byte[2];
            byte[] joint3_arr = new byte[2];
            joint1_arr = BitConverter.GetBytes((Int16)numericUpDown1.Value);
            joint2_arr = BitConverter.GetBytes((Int16)numericUpDown2.Value);
            joint3_arr = BitConverter.GetBytes((Int16)numericUpDown3.Value);
            buffer[1] = joint1_arr[0];
            buffer[2] = joint1_arr[1];
            buffer[3] = joint2_arr[0];
            buffer[4] = joint2_arr[1];
            buffer[5] = joint3_arr[0];
            buffer[6] = joint3_arr[1];
            serialPort1.Write(buffer, 0, buffer.Length);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            byte[] buffer = new byte[1];
            buffer[0] = (byte)0;
            serialPort1.Write(buffer, 0, buffer.Length);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
        }

        private void servo_ping(byte[] buffer)
        {
            for (int servo=1; servo<6; servo++)
            {
                if (buffer[servo] == (byte) 1)
                {
                    textBox1.Text += "Servo " + servo + " OK" + Environment.NewLine;
                }
                else
                {
                    textBox1.Text += "Servo " + servo + " not connected" + Environment.NewLine;
                }
            }
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] buffer = new byte[serialPort1.BytesToRead];
            serialPort1.Read(buffer, 0, buffer.Length);
            switch(buffer[0])
            {
                default:
                    textBox1.Text += "Buffer error: " + buffer[0] + Environment.NewLine;
                    break;
                case ((byte) 0):
                    servo_ping(buffer);
                    break;
            }
        }
    }
}

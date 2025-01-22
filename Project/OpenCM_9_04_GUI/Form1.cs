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
                button4.Enabled = true;
                button5.Enabled = true;
                button6.Enabled = true;
                button7.Enabled = true;
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;
                numericUpDown4.Enabled = true;
                numericUpDown5.Enabled = true;

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
                button4.Enabled = false;
                button5.Enabled = false;
                button6.Enabled = false;
                button7.Enabled = false;
                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;
                numericUpDown3.Enabled = false;
                numericUpDown4.Enabled = false;
                numericUpDown5.Enabled = false;
                numericUpDown1.Value = 0;
                numericUpDown2.Value = 0;
                numericUpDown3.Value = 0;
                numericUpDown4.Value = 0;
                numericUpDown5.Value = 0;

                textBox1.Text += "Port " + serialPort1.PortName.ToString() + " is closed" + Environment.NewLine;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            serialPort1.WriteLine("a" + numericUpDown1.Value);
            textBox1.Text += "Signal to Servo1: Rotate " + numericUpDown1.Value.ToString() + " degrees" + Environment.NewLine;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            serialPort1.WriteLine("b" + numericUpDown2.Value);
            textBox1.Text += "Signal to Servo2: Rotate " + numericUpDown2.Value.ToString() + " degrees" + Environment.NewLine;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            serialPort1.WriteLine("c" + numericUpDown3.Value);
            textBox1.Text += "Signal to Servo3: Rotate " + numericUpDown3.Value.ToString() + " degrees" + Environment.NewLine;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            serialPort1.WriteLine("d" + numericUpDown4.Value);
            textBox1.Text += "Signal to Servo4: Rotate " + numericUpDown4.Value.ToString() + " degrees" + Environment.NewLine;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            serialPort1.WriteLine("e" + numericUpDown5.Value);
            textBox1.Text += "Signal to Servo5: Rotate " + numericUpDown5.Value.ToString() + " degrees" + Environment.NewLine;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            textBox1.Text += "Serial port signal: " + serialPort1.ReadLine() + Environment.NewLine;
        }
    }
}

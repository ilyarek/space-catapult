#include <Dynamixel2Arduino.h>

#define DXL_SERIAL   Serial3 //OpenCM9.04 EXP Board's DXL port Serial. (Serial1 for the DXL port on the OpenCM 9.04 board)
#define DEBUG_SERIAL Serial

const int DXL_DIR_PIN = 22; //OpenCM9.04 EXP Board's DIR PIN. (28 for the DXL port on the OpenCM 9.04 board)
const byte DXL_ID[7] = {18, 5, 14, 11, 15, 2, 3}; // Написать здесь айди мотора, который написан сбоку серво
// снизу вверх, слева направо 1 5 14 11 15 ? ?; 
const float DXL_PROTOCOL_VERSION = 1.0; // Обязательно 1.0!

Dynamixel2Arduino dxl(DXL_SERIAL, DXL_DIR_PIN);

//This namespace is required to use Control table item names
using namespace ControlTableItem;

void setup() {
  
  // Use UART port of DYNAMIXEL Shield to debug.
  DEBUG_SERIAL.begin(115200);
  // Set Port baudrate to 57600bps. This has to match with DYNAMIXEL baudrate.
  dxl.begin(1000000);
  dxl.setPortProtocolVersion(DXL_PROTOCOL_VERSION);

  for (int i=0; i<7; i++)
  {
    dxl.torqueOff(DXL_ID[i]);
    dxl.setOperatingMode(DXL_ID[i], OP_POSITION);
    dxl.torqueOn(DXL_ID[i]);
  }
}


void loop() {
  if (DEBUG_SERIAL.available() > 0)
  {
    byte buffer[9];
    DEBUG_SERIAL.readBytes(buffer, 9);
    switch(buffer[0])
    {
      case ((byte) 0):
        ping();
        break;
      case ((byte) 1):
        move(buffer);
        break;
      case ((byte) 2):
        initial_move();
        break;
      case ((byte) 3):
        setVelocity(buffer);
        break;
    }
    DEBUG_SERIAL.flush();
  }
}

void setVelocity(byte buffer[9])
{
  byte velocity_arr[2] = {buffer[1], buffer[2]};

  short velocity = *((short*)velocity_arr);

  for (int i=0; i<7; i++)
  {
    dxl.setGoalVelocity(DXL_ID[i], velocity); 
  }
}

void ping()
{
  byte buffer[8];
  buffer[0] = (byte) 0;
  for (int i=1; i<8; i++)
  {
    buffer[i] = (byte) dxl.ping(DXL_ID[i-1]);
  }
  DEBUG_SERIAL.write(buffer, 8);
}

void move(byte buffer[9])
{
  byte rotate_arr[2] = {buffer[1], buffer[2]};
  byte joint1_arr[2] = {buffer[3], buffer[4]};
  byte joint2_arr[2] = {buffer[5], buffer[6]};
  byte joint3_arr[2] = {buffer[7], buffer[8]};
  
  short rotate = *((short*)rotate_arr);
  short joint1 = *((short*)joint1_arr);
  short joint2 = *((short*)joint2_arr);
  short joint3 = *((short*)joint3_arr);

  dxl.setGoalPosition(DXL_ID[0], rotate, UNIT_RAW);

  for (int i=1; i<3; i++)
  {
    dxl.setGoalPosition(DXL_ID[i], joint1, UNIT_RAW);
  }

  for (int i=3; i<5; i++)
  {
    dxl.setGoalPosition(DXL_ID[i], joint2, UNIT_RAW);
  }

  for (int i=5; i<7; i++)
  {
    dxl.setGoalPosition(DXL_ID[i], joint3, UNIT_RAW);
  }
}

void initial_move()
{
  short rotate_position = dxl.getPresentPosition(DXL_ID[0], UNIT_RAW);
  short joint1_position = dxl.getPresentPosition(DXL_ID[1], UNIT_RAW);
  short joint2_position = dxl.getPresentPosition(DXL_ID[3], UNIT_RAW);
  short joint3_position = dxl.getPresentPosition(DXL_ID[5], UNIT_RAW);

  byte buffer[9];
  buffer[0] = (byte) 2;
  buffer[1] = (byte)(rotate_position >> 8);
  buffer[2] = (byte)rotate_position;
  buffer[3] = (byte)(joint1_position >> 8);
  buffer[4] = (byte)joint1_position;
  buffer[5] = (byte)(joint2_position >> 8);
  buffer[6] = (byte)joint2_position;
  buffer[7] = (byte)(joint3_position >> 8);
  buffer[8] = (byte)joint3_position;

  DEBUG_SERIAL.write(buffer, 9);
}

/** Please refer to each DYNAMIXEL eManual(http://emanual.robotis.com) for supported Operating Mode
 * Operating Mode
 *  1. OP_POSITION                (Position Mode in protocol2.0, Joint Mode in protocol1.0)
 *  2. OP_VELOCITY                (Velocity Mode in protocol2.0, Speed Mode in protocol1.0)
 *  3. OP_PWM                     (PWM Mode in protocol2.0)
 *  4. OP_EXTENDED_POSITION       (Extended Position Mode in protocol2.0, Multi-turn Mode(only MX series) in protocol1.0)
 *  5. OP_CURRENT                 (Current Mode in protocol2.0, Torque Mode(only MX64,MX106) in protocol1.0)
 *  6. OP_CURRENT_BASED_POSITION  (Current Based Postion Mode in protocol2.0 (except MX28, XL430))
 */

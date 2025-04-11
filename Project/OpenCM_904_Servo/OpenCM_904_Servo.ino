#include <Dynamixel2Arduino.h>

#define DXL_SERIAL   Serial3 //OpenCM9.04 EXP Board's DXL port Serial. (Serial1 for the DXL port on the OpenCM 9.04 board)
#define DEBUG_SERIAL Serial

const int DXL_DIR_PIN = 22; //OpenCM9.04 EXP Board's DIR PIN. (28 for the DXL port on the OpenCM 9.04 board)
const byte DXL_ID[5] = {1, 13, 14, 11, 15}; // Написать здесь айди мотора, который написан сбоку серво
// снизу вверх, справа налево 1 5 14 11 15; не все серво ориентированы одинаково, проверить
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

  for (int i=0; i<5; i++)
  {
    dxl.torqueOff(DXL_ID[i]);
    dxl.setOperatingMode(DXL_ID[i], OP_POSITION);
    dxl.torqueOn(DXL_ID[i]);
  }
}


void loop() {
  if (DEBUG_SERIAL.available() > 0)
  {
    byte buffer[7];
    DEBUG_SERIAL.readBytes(buffer, 7);
    switch(buffer[0])
    {
      case ((byte) 0):
        ping();
        break;
      case ((byte) 1):
        move(buffer);
        break;
    }
    DEBUG_SERIAL.flush();
  }
}

void ping()
{
  byte buffer[6];
  buffer[0] = (byte) 0;
  for (int i=1; i<6; i++)
  {
    buffer[i] = (byte) dxl.ping(DXL_ID[i-1]);
  }
  DEBUG_SERIAL.write(buffer, 6);
}

void move(byte buffer[7])
{
  byte joint1_arr[2] = {buffer[1], buffer[2]};
  byte joint2_arr[2] = {buffer[3], buffer[4]};
  byte joint3_arr[2] = {buffer[5], buffer[6]};
  
  short joint1 = *((short*)joint1_arr);
  short joint2 = *((short*)joint2_arr);
  short joint3 = *((short*)joint3_arr);

  dxl.setGoalPosition(DXL_ID[0], joint1, UNIT_RAW);
  dxl.setGoalPosition(DXL_ID[1], joint1, UNIT_RAW);
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

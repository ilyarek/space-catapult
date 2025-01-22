#include <Dynamixel2Arduino.h>

#define DXL_SERIAL   Serial3 //OpenCM9.04 EXP Board's DXL port Serial. (Serial1 for the DXL port on the OpenCM 9.04 board)
#define DEBUG_SERIAL Serial

const int DXL_DIR_PIN = 22; //OpenCM9.04 EXP Board's DIR PIN. (28 for the DXL port on the OpenCM 9.04 board)
const uint8_t DXL_ID1 = 1; // Написать здесь айди мотора, который написан сбоку серво
const uint8_t DXL_ID2 = 13;
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

  dxl.torqueOff(DXL_ID1);
  dxl.torqueOff(DXL_ID2);
  dxl.setOperatingMode(DXL_ID1, OP_POSITION);
  dxl.setOperatingMode(DXL_ID2, OP_POSITION);
  

  if ( dxl.ping(DXL_ID1) )
  {
    DEBUG_SERIAL.print("Servomotor ");
    DEBUG_SERIAL.print(DXL_ID1);
    DEBUG_SERIAL.println(" Found");
  }
  else
  {
    DEBUG_SERIAL.print("Servomotor ");
    DEBUG_SERIAL.print(DXL_ID1);
    DEBUG_SERIAL.println(" Not found");
  }

  if ( dxl.ping(DXL_ID2) )
  {
    DEBUG_SERIAL.print("Servomotor ");
    DEBUG_SERIAL.print(DXL_ID2);
    DEBUG_SERIAL.println(" Found");
  }
  else
  {
    DEBUG_SERIAL.print("Servomotor ");
    DEBUG_SERIAL.print(DXL_ID2);
    DEBUG_SERIAL.println(" Not found");
  }

  dxl.torqueOn(DXL_ID1);
  dxl.torqueOn(DXL_ID2);

  dxl.setGoalPosition(DXL_ID1, 1.0, UNIT_DEGREE);
  dxl.setGoalPosition(DXL_ID2, 1.0, UNIT_DEGREE);
  
}


void loop() {
  if (DEBUG_SERIAL.available() > 1)
  {
    switchSignal();
    DEBUG_SERIAL.flush();
  }
  //delay(5);
}

void switchSignal()
{
  char key = DEBUG_SERIAL.read();
  switch (key)
    {
      case 'a':
        {
          float val = DEBUG_SERIAL.parseFloat();
          dxl.setGoalPosition(DXL_ID1, val, UNIT_DEGREE);
          DEBUG_SERIAL.print(key);
          DEBUG_SERIAL.println(String(val));
          break;
        }
      case 'b':
        {
          float val = DEBUG_SERIAL.parseFloat();
          dxl.setGoalPosition(DXL_ID2, val, UNIT_DEGREE);
          DEBUG_SERIAL.print(key);
          DEBUG_SERIAL.println(String(val));
          break;
        }
      case 13:
        {
          switchSignal();
          break;
        }
      case 10:
        {
          switchSignal();
          break;
        }
      default:
        {
          float val = DEBUG_SERIAL.parseFloat();
          DEBUG_SERIAL.print("Bytes");
          DEBUG_SERIAL.println(String(DEBUG_SERIAL.available()));
          DEBUG_SERIAL.print("Key");
          DEBUG_SERIAL.println(key);
          DEBUG_SERIAL.print("Val");
          DEBUG_SERIAL.println(String(val));
        }
    }
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

const int PIN_JOY_X = 3;    // D3
const int PIN_JOY_Y = 8;    // D8  
const int PIN_JOY_K = 9;    // D9

void setup() {
  Serial.begin(115200);
  pinMode(PIN_JOY_K, INPUT_PULLUP);
}

void loop() {
  int xVal = analogRead(PIN_JOY_X);
  int yVal = analogRead(PIN_JOY_Y);
  int kVal = digitalRead(PIN_JOY_K);
  
  Serial.print("X: ");
  Serial.print(xVal);
  Serial.print(" | Y: ");
  Serial.print(yVal);
  Serial.print(" | Button: ");
  Serial.println(kVal == LOW ? "PRESSED" : "released");
  
  delay(50);
}
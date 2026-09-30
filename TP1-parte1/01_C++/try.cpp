#include <iostream>

int main()
{
  int num1, num2, sum;

  std::cout << "Introduce el primer número:                                                                                                                               ";
  std::cin >> num1;

  std::cout << "Introduce el segundo número: ";
  std::cin >> num2;

  sum = num1 + num2;

  std::cout << "La suma de " << num1 << " y " << num2 << " es: " << sum << std::endl;

  return 0;
}
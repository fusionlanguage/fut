#include <cstdio>

int abs(int x) { return __builtin_abs(x); }
double acos(double x) { return __builtin_acos(x); }
double asin(double x) { return __builtin_asin(x); }
double atan(double x) { return __builtin_atan(x); }
double atan2(double y, double x) { return __builtin_atan2(y, x); }
double cbrt(double x) { return __builtin_cbrt(x); }
double ceil(double x) { return __builtin_ceil(x); }
double cos(double x) { return __builtin_cos(x); }
double cosh(double x) { return __builtin_cosh(x); }
double exp(double x) { return __builtin_exp(x); }
float fabs(float x) { return __builtin_fabs(x); }
double fabs(double x) { return __builtin_fabs(x); }
float floor(float x) { return __builtin_floor(x); }
double floor(double x) { return __builtin_floor(x); }
double fma(double x, double y, double z) { return __builtin_fma(x, y, z); }
float fmax(float x, float y) { return __builtin_fmax(x, y); }
double fmax(double x, double y) { return __builtin_fmax(x, y); }
float fmin(float x, float y) { return __builtin_fmin(x, y); }
double fmin(double x, double y) { return __builtin_fmin(x, y); }
bool isfinite(float x) { return __builtin_isfinite(x); }
bool isinf(float x) { return __builtin_isinf(x); }
bool isnan(float x) { return __builtin_isnan(x); }
bool isnan(double x) { return __builtin_isnan(x); }
double log(double x) { return __builtin_log(x); }
double log2(double x) { return __builtin_log2(x); }
double log10(double x) { return __builtin_log10(x); }
double max(int x, int y) { return x < y ? y : x; }
double min(int x, int y) { return y < x ? y : x; }
double pow(double x, double y) { return __builtin_pow(x, y); }
double sin(double x) { return __builtin_sin(x); }
double sinh(double x) { return __builtin_sinh(x); }
double sqrt(double x) { return __builtin_sqrt(x); }
double tan(double x) { return __builtin_tan(x); }
double tanh(double x) { return __builtin_tanh(x); }
double trunc(double x) { return __builtin_trunc(x); }

extern "C" bool Test_Run();

int main()
{
	if (Test_Run()) {
		puts("PASSED");
		return 0;
	}
	else {
		puts("FAILED");
		return 1;
	}
}

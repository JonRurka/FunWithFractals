using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.IO;
using System.Linq;
using UnityEngine;

public class MandelCalc : MonoBehaviour {

    public enum ComputeMethodSelect
    {
        CPU,
        UnityCompute,
        OpenCL
    }

    public class Complex
    {
        public double Real;
        public double Imaginary;

        public double R { get { return Abs(); } }
        public double Theta
        {
            get
            {
                return System.Math.Atan2(Imaginary, Real);
            }
        }

        public Complex(double real, double imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public Complex(Complex orig)
        {
            Real = orig.Real;
            Imaginary = orig.Imaginary;
        }

        public void Add(Complex b)
        {
            Real = Real + b.Real;
            Imaginary = Imaginary + b.Imaginary;
        }

        public void Subtract(Complex b)
        {
            Real = Real - b.Real;
            Imaginary = Imaginary - b.Imaginary;
        }

        public double Abs()
        {
            return System.Math.Sqrt(Real * Real + Imaginary * Imaginary);
        }

        public double AbsNoSqrt()
        {
            return Real * Real + Imaginary * Imaginary;
        }

        public override string ToString()
        {
            return string.Format("({0} + {1}i)", Real, Imaginary);
        }

        public void Multiply(Complex w)
        {
            //Real = Real * w.Real - Imaginary * w.Imaginary;
            //Imaginary = Real * w.Imaginary + Imaginary * w.Real;
            double r1 = Real * w.Real;
            double r2 = Real * w.Imaginary;
            double r3 = Imaginary * w.Real;
            double r4 = -(Imaginary * w.Imaginary);

            Matrix4x4 mat1 = new Matrix4x4(new Vector4((float)Real, (float)Imaginary, 0, 0), Vector4.zero, Vector4.zero, Vector4.zero);
            Matrix4x4 mat2 = new Matrix4x4(new Vector4((float)w.Real, (float)w.Imaginary, 0, 0), Vector4.zero, Vector4.zero, Vector4.zero);
            Matrix4x4 res = mat1 * mat2;

            Real = res.m00;
            Imaginary = res.m01;
        }

        public void Multiply(double b)
        {
            Real = Real * b;
            Imaginary = Imaginary * b;
        }

        public void Divide(Complex b)
        {
            double dividend = (Real * b.Real + Imaginary * b.Imaginary) + (-(Real * b.Imaginary) + Imaginary * b.Real);
            double divisor = b.Real * b.Real + b.Imaginary * b.Imaginary;
            Real = dividend / divisor;
            Imaginary = 0;
        }

        public void Pow(double pow)
        {
            // z^n = r^n (cos(nθ) + i sin(nθ))
            double z1 = System.Math.Pow(R, pow) * System.Math.Cos(pow * Theta);
            double z2 = System.Math.Pow(R, pow) * System.Math.Sin(pow * Theta);
            Real = z1;
            Imaginary = z2;
        }

        public static Complex Add(Complex a, Complex b)
        {
            return new Complex(a.Real + b.Real, a.Imaginary + b.Imaginary);
        }

        public static Complex Subtract(Complex a, Complex b)
        {
            return new Complex(a.Real - b.Real, a.Imaginary - b.Imaginary);
        }

        public static Complex Abs(Complex a)
        {
            return new Complex(System.Math.Sqrt(a.Real * a.Real + a.Imaginary * a.Imaginary), 0);
        }

        public static Complex Multiply(Complex z, Complex w)
        {
            // zw = (x + yi)(u + vi) = (xu – yv) + (xv + yu)i
            return new Complex(z.Real * w.Real - z.Imaginary * w.Imaginary, z.Real * w.Imaginary + z.Imaginary * w.Real);

            /*float r1 = a.Real * b.Real;
            float r2 = a.Real * b.Imaginary;
            float r3 = a.Imaginary * b.Real;
            float r4 = -(a.Imaginary * b.Imaginary);
            return new Complex(r1 + r4, r2 + r3);*/

        }

        public static Complex Multiply(Complex a, float b)
        {
            return new Complex(a.Real * b, a.Imaginary * b);
        }

        public static Complex Divide(Complex a, Complex b)
        {
            double dividend = (a.Real * b.Real + a.Imaginary * b.Imaginary) + (-(a.Real * b.Imaginary) + a.Imaginary * b.Real);
            double divisor = b.Real * b.Real + b.Imaginary * b.Imaginary;
            return new Complex(dividend / divisor, 0);
        }
    }

    static string IsPrime
    {
        get
        {
            return @"
            kernel void GetIfPrime(global int* message)
            {
                int index = get_global_id(0);

                int upperl=(int)sqrt((float)message[index]);
                for(int i=2;i<=upperl;i++)
                {
                    if(message[index]%i==0)
                    {
                        //printf("" %d / %d\n"",index,i );
                        message[index]=0;
                        return;
                    }
                }
                //printf("" % d"",index);
            }";
        }
    }

    public class SciNum
    {
        public long Exp;
        public double Man;
        public double Value { get { return ToNormal(Man, Exp); } }

        public SciNum()
        {
            Exp = 1;
            Man = 0;
        }

        public SciNum(double value)
        {
            Exp = Exponent(value);
            Man = Mantissa(value, Exp);
        }

        public SciNum(double m, long e)
        {
            Exp = e;
            Man = m;
        }

        public SciNumTransfer GetTransferStruct()
        {
            return new SciNumTransfer(this);
        }

        public static SciNum operator + (SciNum f, SciNum s)
        {
            return f.Add(s);
        }

        public static SciNum operator -(SciNum f, SciNum s)
        {
            return f.Subtract(s);
        }

        public static SciNum operator *(SciNum f, SciNum s)
        {
            return f.Multiply(s);
        }

        public static SciNum operator /(SciNum f, SciNum s)
        {
            return f.Divide(s);
        }

        public static bool operator >(SciNum f, SciNum s)
        {
            

            long diff = f.Exp - s.Exp;

            if (System.Math.Abs(diff) > 1)
                return (f.Exp > s.Exp);

            long otherExp = f.Exp;
            double otherM = s.Man / System.Math.Pow(10, diff);

            return f.Man > otherM;
            


            //bool greaterExp = f.Exp > s.Exp;
            //bool equalGreaterExp = f.Exp >= s.Exp;
            //return (greaterExp || (f.Man > s.Man && equalGreaterExp));
        }

        public static bool operator <(SciNum f, SciNum s)
        {
            long diff = f.Exp - s.Exp;

            if (System.Math.Abs(diff) > 1)
                return (f.Exp > s.Exp);

            long otherExp = f.Exp;
            double otherM = s.Man / System.Math.Pow(10, diff);

            return f.Man < otherM;



            /*bool lesserExp = f.Exp < s.Exp;
            bool equalLesserExp = f.Exp <= s.Exp;
            bool lessMan = f.Man < s.Man;
            return (lesserExp || (lessMan && equalLesserExp));*/
        }

        public static bool operator ==(SciNum f, SciNum s)
        {
            return (f.Exp == s.Exp && f.Man == s.Man);
        }

        public static bool operator !=(SciNum f, SciNum s)
        {
            return (f.Exp != s.Exp || f.Man != s.Man);
        }

        public static bool operator <=(SciNum f, SciNum s)
        {
            return (f < s || f == s);
        }

        public static bool operator >=(SciNum f, SciNum s)
        {
            return (f > s || f == s);
        }

        public bool ManValid()
        {
            return ManValid(Man);
        }

        public bool ExpValid()
        {
            return ExpValid(Exp);
        }

        public bool IsValid()
        {
            return ManValid() && ExpValid();
        }

        public void Fix()
        {
            long manExp = Exponent(Man);
            if (ExpValid(manExp))
            {
                if (System.Math.Abs(manExp) > 0)
                {
                    Man = Man * System.Math.Pow(10, -manExp);
                    Exp = Exp + manExp;
                }
            }

        }

        public override string ToString()
        {
            return string.Format("{0}x10^{1}", Man, Exp);
        }

        private long Exponent(double d)
        {
            return (long)System.Math.Log10(System.Math.Abs(d));
        }

        private double Mantissa(double d, long exp)
        {
            return d / System.Math.Pow(10, exp);
        }

        private double ToNormal(double m, long exp)
        {
            return m * System.Math.Pow(10, exp);
        }

        private SciNum Add(SciNum other)
        {
            long diff = Exp - other.Exp;
            long otherExp = Exp;
            double otherM = other.Man / System.Math.Pow(10, diff);
            return new SciNum(Man + otherM, otherExp);
        }

        private SciNum Subtract(SciNum other)
        {
            long diff = Exp - other.Exp;
            long otherExp = Exp;
            double otherM = other.Man / System.Math.Pow(10, diff);
            return new SciNum(Man - otherM, otherExp);
        }

        private SciNum Multiply(SciNum other)
        {
            return new SciNum(Man * other.Man, Exp + other.Exp);
        }

        private SciNum Divide(SciNum other)
        {
            return new SciNum(Man / other.Man, Exp - other.Exp);
        }

        private bool ManValid(double man)
        {
            return !double.IsNaN(man) && !double.IsInfinity(man) && !double.IsNegativeInfinity(man) && !double.IsPositiveInfinity(man);
        }

        private bool ExpValid(long exp)
        {
            return exp != long.MaxValue && exp != long.MinValue;
        }

        public override bool Equals(object obj)
        {
            var num = obj as SciNum;
            return num != null &&
                   Exp == num.Exp &&
                   Man == num.Man;
        }

        public override int GetHashCode()
        {
            var hashCode = -1791369447;
            hashCode = hashCode * -1521134295 + Exp.GetHashCode();
            hashCode = hashCode * -1521134295 + Man.GetHashCode();
            return hashCode;
        }
    }

    public struct SciNumTransfer
    {
        public const int SIZE = 12;
        public int Exp;
        public System.UInt32 M1;
        public System.UInt32 M2;

        public SciNumTransfer(SciNum val)
        {
            Exp = (int)val.Exp;
            byte[] manBuff = System.BitConverter.GetBytes(val.Man);
            M1 = System.BitConverter.ToUInt32(manBuff, 0);
            M2 = System.BitConverter.ToUInt32(manBuff, 4);
        }
    }

    public ComputeMethodSelect method = ComputeMethodSelect.UnityCompute;
    public bool Julia = false;
    public ComputeShader shader;

    public int width = 100;
    public int height = 100;
    private int curWidth = 0;
    private int curHeight = 0;

    public double pow = 6;
    public double powSpeed = 0.1f;

    public double xMinDefault = -2.1f;
    public double xMaxDefault = 1f;

    public double yMinDefault = -1.3f;
    public double yMaxDefault = 1.3f;

    public double julia_x = -0.516;
    public double julia_y = 0.1023;
    public double speed = .01f;

    public int iMaxDefault = 1000;

    public List<Color> gradient;
    public bool grayScale = false;

    private Texture2D guiTex;

    public double xLen;
    public double yLen;

    public int iMax;
    public int zoom = 0;

    public bool mark;
    public double markX = 0;
    public double markY = 0;

    RenderTexture tex;

    private SciNum xMin;
    private SciNum xMax;

    private SciNum yMin;
    private SciNum yMax;

    bool changed = false;
    public bool calculating = false;
    public string LastProcessTime;
    public bool useImage;
    public Texture inTex;
    public float imageStrength = 100;
    bool screenshotNextFrame = false;
    bool getMousePos = false;


    System.Action mainThr;

    System.Diagnostics.Stopwatch watch;

    const float e = 2.71828f;

    // Use this for initialization
    void Start () {
        /*float i = Mathf.Sqrt(-1);
        float x = 0.1f;

        float firstPart = Mathf.Pow(e, i * x);
        float secondPart = Mathf.Cos(x) + i * Mathf.Sin(x);

        Debug.LogFormat("equal? {0}, {1}, {2}", (firstPart == secondPart), firstPart, secondPart);*/

        /*float pow = 5;

        Complex c1 = new Complex(0.1, 5);
        c1.Pow(pow);
        Debug.LogFormat("{0} + {1}i", c1.Real, c1.Imaginary);

        Complex c2 = new Complex(0.1, 5);
        Complex orig = new Complex(0.1, 5);
        for (int j = 0; j < pow - 1; j++)
            c2.Multiply(orig);
        Debug.LogFormat("{0} + {1}i", c2.Real, c2.Imaginary);*/


        ResetToDefault();
    }

    void Update()
    {
        SciNum dt = new SciNum(Time.deltaTime);
        SciNum ten = new SciNum(10);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            curWidth = width;
            curHeight = height;
            guiTex = new Texture2D(curWidth, curHeight);
            InitRenderTexture();
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax, true);
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            curWidth = width;
            curHeight = height;
            guiTex = new Texture2D(curWidth, curHeight);
            InitRenderTexture();
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            yMin += ((yMax - yMin) / ten) * dt;
            yMax += ((yMax - yMin) / ten) * dt;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            yMin -= ((yMax - yMin) / ten) * dt;
            yMax -= ((yMax - yMin) / ten) * dt;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            xMin -= ((xMax - xMin) / ten) * dt;
            xMax -= ((xMax - xMin) / ten) * dt;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            xMin += ((xMax - xMin) / ten) * dt;
            xMax += ((xMax - xMin) / ten) * dt;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }

        if (Input.GetKey(KeyCode.W))
        {
            yMin += ((yMax - yMin) / ten) * dt;
            yMax -= ((yMax - yMin) / ten) * dt;
            xMin += ((xMax - xMin) / ten) * dt;
            xMax -= ((xMax - xMin) / ten) * dt;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
            zoom++;
        }
        if (Input.GetKey(KeyCode.S))
        {
            yMin -= ((yMax - yMin) / ten) * dt;
            yMax += ((yMax - yMin) / ten) * dt;
            xMin -= ((xMax - xMin) / ten) * dt;
            xMax += ((xMax - xMin) / ten) * dt;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
            zoom--;
        }

        if (Input.GetKey(KeyCode.Z))
        {
            pow -= Time.deltaTime * powSpeed;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }
        if (Input.GetKey(KeyCode.X))
        {
            pow += Time.deltaTime * powSpeed;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }

        if (Input.GetKey(KeyCode.I))
        {
            julia_y += Time.deltaTime * speed;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }
        if (Input.GetKey(KeyCode.K))
        {
            julia_y -= Time.deltaTime * speed;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }
        if (Input.GetKey(KeyCode.J))
        {
            julia_x -= Time.deltaTime * speed;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }
        if (Input.GetKey(KeyCode.L))
        {
            julia_x += Time.deltaTime * speed;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }

        if (Input.GetMouseButtonDown(1))
        {
            getMousePos = true;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
            getMousePos = false;
            CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
        }

        if (mainThr != null)
        {
            mainThr();
            mainThr = null;
        }
    }

    void OnGUI()
    {
        if (guiTex != null)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), guiTex);
        }

        GUI.Window(0, new Rect(0, 3 * (Screen.height / 4f), 570, Screen.height / 4f), UI, "Settings");
    }

    int method_select = 1;
    int set_select = 0;
    string widthStr = "";
    string heightStr = "";
    string x_min_str = "";
    string x_max_str = "";
    string y_min_str = "";
    string y_max_str = "";
    string julia_x_str = "";
    string julia_y_str = "";
    string powStr = "";
    string powSpeedStr = "";
    string speedStr = "";
    string imaxStr = "";
    string markxStr = "";
    string markyStr = "";
    void UI(int id)
    {
        // first column
        float x = 30;
        GUI.Label(new Rect(x, 20, 150, 20), "Compute Method:");
        method_select = GUI.SelectionGrid(new Rect(x + 10, 40, 100, 70), method_select, new string[] { "CPU", "Unity Compute", "OpenCL" }, 1, "toggle");
        switch (method_select)
        {
            case 0:
                method = ComputeMethodSelect.CPU;
                break;
            case 1:
                method = ComputeMethodSelect.UnityCompute;
                break;
            case 2:
                method = ComputeMethodSelect.OpenCL;
                break;
        }

        GUI.Label(new Rect(x, 130, 150, 20), "Fractal Set:");
        set_select = GUI.SelectionGrid(new Rect(x + 10, 150, 100, 40), set_select, new string[] { "Mandelbrot", "Julia"}, 1, "toggle");
        switch (set_select)
        {
            case 0:
                Julia = false;
                break;
            case 1:
                Julia = true;
                break;
        }

        // second column
        x = 200;
        GUI.Label(new Rect(x, 20, 150, 20), "Width:");
        widthStr = GUI.TextField(new Rect(x + 45, 20, 50, 20), widthStr);
        int tmpWidth;
        if (int.TryParse(widthStr, out tmpWidth))
            width = tmpWidth;

        GUI.Label(new Rect(x, 40, 150, 20), "Height:");
        heightStr = GUI.TextField(new Rect(x + 45, 40, 50, 20), heightStr);
        int tmpHeight;
        if (int.TryParse(heightStr, out tmpHeight))
            height = tmpHeight;

        GUI.Label(new Rect(x, 80, 150, 20), "X Min:");
        x_min_str = GUI.TextField(new Rect(x + 45, 80, 50, 20), x_min_str);
        double tmp;
        if (double.TryParse(CheckStr(x_min_str), out tmp))
            xMin = new SciNum(tmp);

        GUI.Label(new Rect(x, 100, 150, 20), "X Max:");
        x_max_str = GUI.TextField(new Rect(x + 45, 100, 50, 20), x_max_str);
        tmp = 0;
        if (double.TryParse(CheckStr(x_max_str), out tmp))
            xMax = new SciNum(tmp);

        GUI.Label(new Rect(x, 120, 150, 20), "Y Min:");
        y_min_str = GUI.TextField(new Rect(x + 45, 120, 50, 20), y_min_str);
        tmp = 0;
        if (double.TryParse(CheckStr(y_min_str), out tmp))
            yMin = new SciNum(tmp);

        GUI.Label(new Rect(x, 140, 150, 20), "Y Max:");
        y_max_str = GUI.TextField(new Rect(x + 45, 140, 50, 20), y_max_str);
        tmp = 0;
        if (double.TryParse(CheckStr(y_max_str), out tmp))
            yMax = new SciNum(tmp);

        GUI.Label(new Rect(x, 180, 150, 20), "Julia X:");
        julia_x_str = GUI.TextField(new Rect(x + 45, 180, 50, 20), julia_x_str);
        tmp = 0;
        if (double.TryParse(CheckStr(julia_x_str), out tmp))
            julia_x = tmp;


        GUI.Label(new Rect(x, 200, 150, 20), "Julia Y:");
        julia_y_str = GUI.TextField(new Rect(x + 45, 200, 50, 20), julia_y_str);
        tmp = 0;
        if (double.TryParse(CheckStr(julia_y_str), out tmp))
            julia_x = tmp;

        // third column
        x = 370;
        GUI.Label(new Rect(x, 20, 150, 20), "Power:");
        powStr = GUI.TextField(new Rect(x + 70, 20, 50, 20), powStr);
        tmp = 0;
        if (double.TryParse(CheckStr(powStr), out tmp))
            pow = tmp;

        GUI.Label(new Rect(x, 40, 150, 20), "Pow speed:");
        powSpeedStr = GUI.TextField(new Rect(x + 70, 40, 50, 20), powSpeedStr);
        tmp = 0;
        if (double.TryParse(CheckStr(powSpeedStr), out tmp))
            powSpeed = tmp;

        GUI.Label(new Rect(x, 80, 150, 20), "Speed:");
        speedStr = GUI.TextField(new Rect(x + 70, 80, 50, 20), speedStr);
        tmp = 0;
        if (double.TryParse(CheckStr(speedStr), out tmp))
            speed = tmp;

        GUI.Label(new Rect(x, 120, 150, 20), "I Max:");
        imaxStr = GUI.TextField(new Rect(x + 70, 120, 50, 20), imaxStr);
        int imaxtmp;
        if (int.TryParse(imaxStr, out imaxtmp))
            iMax = imaxtmp;

        GUI.Label(new Rect(x, 160, 150, 20), "Mark:");
        mark = GUI.Toggle(new Rect(x + 70, 160, 150, 20), mark, "");

        GUI.Label(new Rect(x, 180, 150, 20), "Mark X:");
        markxStr = GUI.TextField(new Rect(x + 70, 180, 100, 20), markxStr);
        if (double.TryParse(CheckStr(markxStr), out tmp))
            markX = tmp;

        GUI.Label(new Rect(x, 200, 150, 20), "Mark Y:");
        markyStr = GUI.TextField(new Rect(x + 70, 200, 100, 20), markyStr);
        tmp = 0;
        if (double.TryParse(CheckStr(markyStr), out tmp))
            markY = tmp;
    }
    void FlushUI()
    {
        widthStr = width.ToString();
        heightStr = height.ToString();
        x_min_str = xMin.Value.ToString();
        x_max_str = xMax.Value.ToString();
        y_min_str = yMin.Value.ToString();
        y_max_str = yMax.Value.ToString();
        julia_x_str = julia_x.ToString();
        julia_y_str = julia_y.ToString();
        powStr = pow.ToString();
        powSpeedStr = powSpeed.ToString();
        speedStr = speed.ToString();
        imaxStr = iMax.ToString();
        markxStr = markX.ToString();
        markyStr = markY.ToString();
    }
    string CheckStr(string input)
    {
        if (input.Trim().EndsWith("."))
            return input + ".0";
        if (input.Trim() == string.Empty)
            return "0";
        return input;
    }

    void OnPostRender()
    {
        if (changed)
        {
            changed = false;
            //Debug.LogFormat("{0}x{1}, {2}x{3}, {4}x{5}", guiTex.width, guiTex.height, tex.width, tex.height, width, height);
            RenderTexture cur = RenderTexture.active;
            RenderTexture.active = tex;
            guiTex.ReadPixels(new Rect(0, 0, curWidth, curHeight), 0, 0);
            guiTex.Apply();
            RenderTexture.active = cur;

            if (screenshotNextFrame)
            {
                screenshotNextFrame = false;
                Save(guiTex);
            }

            watch.Stop();
            //Debug.Log("GPU processed: " + watch.Elapsed);
            LastProcessTime = watch.Elapsed.ToString();
            watch.Reset();
        }
    }

    public void ResetToDefault()
    {
        curWidth = width;
        curHeight = height;
        guiTex = new Texture2D(curWidth, curHeight);
        InitRenderTexture();
        xMin = new SciNum(xMinDefault);
        xMax = new SciNum(xMaxDefault);
        yMin = new SciNum(yMinDefault);
        yMax = new SciNum(yMaxDefault);
        iMax = iMaxDefault;
        watch = new System.Diagnostics.Stopwatch();
        CheckUpdateTexture(xMin, xMax, yMin, yMax, iMax);
    }

    public void CheckUpdateTexture(SciNum xMin, SciNum xMax, SciNum yMin, SciNum yMax, int iMax, bool screenshot = false)
    {
        FlushUI();

        if (iMax < 1)
            throw new System.Exception("iMax must be greater than 0.");

        switch (method)
        {
            case ComputeMethodSelect.UnityCompute:
                UpdateTexture_Gpu(xMin, xMax, yMin, yMax, iMax, screenshot);
                break;
            case ComputeMethodSelect.OpenCL:
                UpdateTexture_CL(xMin, xMax, yMin, yMax, iMax, screenshot);
                break;
            case ComputeMethodSelect.CPU:
                if (!calculating)
                    UpdateTexture(xMin, xMax, yMin, yMax, iMax, screenshot);
                break;
        }
    }

    public void UpdateTexture_Gpu(SciNum xMin, SciNum xMax, SciNum yMin, SciNum yMax, int iMax, bool screenshot = false)
    {
        if (shader == null)
            return;
        watch = new System.Diagnostics.Stopwatch();
        watch.Start();

        xLen = (xMax - xMin).Value / width;
        yLen = (yMax - yMin).Value / height;

        int kernel = shader.FindKernel("CSMain");

        //Debug.LogFormat("{0}, {1}, {2}, {3}", xMin.Value, xMax.Value, yMin.Value, yMax.Value);

        byte[] xMinBuff = System.BitConverter.GetBytes(xMin.Value);
        byte[] xMaxBuff = System.BitConverter.GetBytes(xMax.Value);
        byte[] yMinBuff = System.BitConverter.GetBytes(yMin.Value);
        byte[] yMaxBuff = System.BitConverter.GetBytes(yMax.Value);

        float[]  view = new float[]
        {
            System.BitConverter.ToUInt32(xMinBuff, 0), System.BitConverter.ToUInt32(xMinBuff, 4),
            System.BitConverter.ToUInt32(xMaxBuff, 0), System.BitConverter.ToUInt32(xMaxBuff, 4),
            System.BitConverter.ToUInt32(yMinBuff, 0), System.BitConverter.ToUInt32(yMinBuff, 4),
            System.BitConverter.ToUInt32(yMaxBuff, 0), System.BitConverter.ToUInt32(yMaxBuff, 4),
        };

        SciNumTransfer[] viewport = new SciNumTransfer[]
        {
            xMin.GetTransferStruct(),
            xMax.GetTransferStruct(),
            yMin.GetTransferStruct(),
            yMax.GetTransferStruct(),
        };

        byte[] pow1buff = System.BitConverter.GetBytes(pow);
        byte[] markXBuff = System.BitConverter.GetBytes(markX);
        byte[] markYBuff = System.BitConverter.GetBytes(markY);

        Vector2 mouse = Input.mousePosition;

        float[] variables = new float[]
        {
            width,
            height,
            iMax,
            gradient.Count,
            System.BitConverter.ToUInt32(pow1buff, 0), System.BitConverter.ToUInt32(pow1buff, 4),
            System.BitConverter.ToUInt32(markXBuff, 0), System.BitConverter.ToUInt32(markXBuff, 4),
            System.BitConverter.ToUInt32(markYBuff, 0), System.BitConverter.ToUInt32(markYBuff, 4),
            (float)julia_x,
            (float)julia_y,
            mouse.x,
            mouse.y,
            0,
            imageStrength
        };


        ComputeBuffer viewBuff = new ComputeBuffer(view.Length, sizeof(float));
        ComputeBuffer varBuff = new ComputeBuffer(variables.Length, sizeof(float));
        ComputeBuffer gradBuff = new ComputeBuffer(gradient.Count, sizeof(float) * 4);
        viewBuff.SetData(view);
        varBuff.SetData(variables);
        gradBuff.SetData(gradient.ToArray());
        shader.SetBuffer(kernel, "viewport", viewBuff);
        shader.SetBuffer(kernel, "variables", varBuff);
        shader.SetBuffer(kernel, "gradient", gradBuff);
        shader.SetBool("julia", Julia);
        shader.SetBool("showMark", mark);
        shader.SetBool("getMouse", getMousePos);
        shader.SetBool("grayScale", grayScale);
        shader.SetBool("useImage", useImage);
        shader.SetTexture(kernel, "Result", tex);
        shader.SetTexture(kernel, "image", inTex);
        shader.Dispatch(kernel, width / 16, height / 16, 1);
        varBuff.GetData(variables);

        if (getMousePos)
        {
            markX = variables[12];
            markY = variables[13];
            DebugPrint(markX, markY, variables[14]);
        }

        viewBuff.Dispose();
        varBuff.Dispose();
        gradBuff.Dispose();

        changed = true;
        screenshotNextFrame = screenshot;

    }

    public void UpdateTexture_CL(SciNum xMin, SciNum xMax, SciNum yMin, SciNum yMax, int iMax, bool screenshot = false)
    {
        int[] Primes = Enumerable.Range(2, 1000000).ToArray();



        double sf = EasyCL.GetDeviceGFlops_Single(AcceleratorDevice.GPU);
        double df = EasyCL.GetDeviceGFlops_Double(AcceleratorDevice.GPU);
        double gbps = EasyCL.GetDeviceBandwidth_GBps(AcceleratorDevice.GPU);

        Debug.LogFormat("GPU: double: {0}, single: {1}, GBps: {2}", sf, df, gbps);

        //sf = EasyCL.GetDeviceGFlops_Single(AcceleratorDevice.CPU);
        //df = EasyCL.GetDeviceGFlops_Double(AcceleratorDevice.CPU);
        //gbps = EasyCL.GetDeviceBandwidth_GBps(AcceleratorDevice.CPU);

        //Debug.LogFormat("CPU: double: {0}, single: {1}, GBps: {2}", sf, df, gbps);

        /*EasyCL cl = new EasyCL();
        cl.Accelerator = AcceleratorDevice.GPU;
        cl.LoadKernel(IsPrime);
        cl.Invoke("GetIfPrime", 0, Primes.Length, Primes);

        StringBuilder str = new StringBuilder();
        for (int i = 0; i < Primes.Length; i++)
        {
            str.AppendFormat("{0}, ", Primes[i]);
        }
        Debug.Log(str);*/


    }

    public void UpdateTexture_sci(SciNum xMin, SciNum xMax, SciNum yMin, SciNum yMax, int iMax, bool screenshot = false)
    {
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        Debug.Log("Running...");
        //System.Action act = () =>
        //{
            calculating = true;
            SciNum xLen = (xMax - xMin) / new SciNum(width);
            SciNum yLen = (yMax - yMin) / new SciNum(width);

            SciNum x = xMin;
            SciNum y = yMin;
            int s = 0;
            int z = 0;
            double xx = 0;

            if (gradient.Count == 0)
            {
                return;
            }

            Color[] colors = new Color[width * height];

            double gp = 1d / gradient.Count;
            for (s = 0; s < width; s++)
            {
                y = yMin;
                for (z = 0; z < height; z++)
                {
                    List<SciNum> values = new List<SciNum>();

                    SciNum x1 = new SciNum();
                    SciNum y1 = new SciNum();
                    SciNum val = new SciNum();
                    int i = 0;
                    while (i < iMax && val.IsValid() && val < new SciNum(4))
                    {
                        i++;
                        SciNum resX = (x1 * x1) - (y1 * y1);
                        SciNum resY = new SciNum(2) * x1 * y1;

                        x1 = resX + x;
                        y1 = resY + y;

                        val = ((x1 * x1) + (y1 * y1));
                        val.Fix();

                        values.Add(val);
                    }

                    /*StringBuilder str = new StringBuilder();
                    for (int j = 0; j < values.Count; j++)
                    {
                        str.AppendFormat("{0}, ", values[j]);
                    }
                    Debug.LogFormat("{0} x {1}: {2}", x.ToString(), y.ToString(), str.ToString());*/


                    double perc = System.Math.Max(1d - (i / (double)iMax), 0);
                    double gd = perc / gp;
                    int gi = (int)gd;
                    try
                    {
                        Color c1 = gradient[Mathf.Min(gi, gradient.Count - 1)];
                        Color c2 = gradient[Mathf.Min(gi + 1, gradient.Count - 1)];
                        Color final = Color.Lerp(c1, c2, (float)(gd - System.Math.Truncate(gd)));
                        colors[z * width + s] = final;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogErrorFormat("{0}, {1}, {2}, {3}", gd, gi, perc, gp);
                        throw ex;
                    }
                    y += yLen;
                }
                x += xLen;
            }
            
            //mainThr = () =>
            //{
                this.iMax++;
                Texture2D tex = new Texture2D(width, height);
                tex.SetPixels(colors);
                tex.Apply();
                guiTex = tex;
                watch.Stop();
                Debug.Log("processed: " + watch.Elapsed);
                calculating = false;

        if (screenshot)
            Save(tex);
        //};
        //};
        //Thread thr = new Thread(() => act());
        //thr.Start();
    }

    public void UpdateTexture(SciNum xMin, SciNum xMax, SciNum yMin, SciNum yMax, int iMax, bool screenshot = false)
    {
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        Debug.Log("Running...");
        //System.Action act = () =>
        //{
        calculating = true;
        double xLen = (xMax.Value - xMin.Value) / width;
        double yLen = (yMax.Value - yMin.Value) / width;

        double x = xMin.Value;
        double y = yMin.Value;
        int s = 0;
        int z = 0;
        double xx = 0;

        if (gradient.Count == 0)
        {
            return;
        }

        Color[] colors = new Color[width * height];
        
        double gp = 1d / gradient.Count;
        for (s = 0; s < width; s++)
        {
            y = yMin.Value;
            for (z = 0; z < height; z++)
            {

                // julia
                //Complex coordx = new Complex(.25, 0);
                //Complex coordy = new Complex(.1023, 0);
                //Complex coord = new Complex(julia_x, julia_y);
                //Complex val = new Complex(x, y);

                Complex coord;
                Complex val;
                if (Julia)
                {
                    coord = new Complex(julia_x, julia_y);
                    val = new Complex(x, y);
                }
                else
                {
                    coord = new Complex(x, y);
                    val = new Complex(0, 0);
                }


                int i = 0;
                while (i < iMax && val.AbsNoSqrt() < 4)
                {
                    i++;
                    Complex orig = new Complex(val);
                    val.Pow(pow);
                    val.Add(coord);
                }

                /*StringBuilder str = new StringBuilder();
                for (int j = 0; j < values.Count; j++)
                {
                    str.AppendFormat("{0}, ", values[j]);
                }
                Debug.LogFormat("{0} x {1}: {2}", x.ToString(), y.ToString(), str.ToString());*/


                double perc = System.Math.Max(1d - (i / (double)iMax), 0);
                double gd = perc / gp;
                int gi = (int)gd;
                try
                {
                    Color c1 = gradient[Mathf.Min(gi, gradient.Count - 1)];
                    Color c2 = gradient[Mathf.Min(gi + 1, gradient.Count - 1)];
                    Color final = Color.Lerp(c1, c2, (float)(gd - System.Math.Truncate(gd)));
                    colors[z * width + s] = final;
                }
                catch (System.Exception ex)
                {
                    Debug.LogErrorFormat("{0}, {1}, {2}, {3}, {4}", gd, gi, perc, gp, iMax);
                    throw ex;
                }
                y += yLen;
            }
            x += xLen;
        }

        //mainThr = () =>
        //{
        this.iMax++;
        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(colors);
        tex.Apply();
        guiTex = tex;
        watch.Stop();
        Debug.Log("processed: " + watch.Elapsed);
        calculating = false;


        if (screenshot)
            Save(tex);
        
        //};
        //};
        //Thread thr = new Thread(() => act());
        //thr.Start();
    }

    public void DebugPrint(int px, int py)
    {
        double x_min = xMin.Value;
        double x_max = xMax.Value;
        double y_min = yMin.Value;
        double y_max = yMax.Value;

        double x = x_min + (px * xLen);
        double y = y_min + (py * yLen);

        markX = x;
        markY = y;

        Complex coord;
        Complex val;
        if (Julia)
        {
            coord = new Complex(julia_x, julia_y);
            val = new Complex(x, y);
        }
        else
        {
            coord = new Complex(x, y);
            val = new Complex(julia_x, julia_y);
        }

        StringBuilder str = new StringBuilder();
        int i = 0;
        while (i < iMax && val.AbsNoSqrt() < 4)
        {
            i++;
            val.Pow(pow);
            val.Add(coord);
            str.Append(val.ToString() + ", ");
        }
        Debug.LogFormat("{0}, {1}: {2}", coord, i, str.ToString());

    }

    public void DebugPrint(double x, double y, float di)
    {
        double x_min = xMin.Value;
        double x_max = xMax.Value;
        double y_min = yMin.Value;
        double y_max = yMax.Value;

        markX = x;
        markY = y;

        Complex coord;
        Complex val;
        if (Julia)
        {
            coord = new Complex(julia_x, julia_y);
            val = new Complex(x, y);
        }
        else
        {
            coord = new Complex(x, y);
            val = new Complex(julia_x, julia_y);
        }

        StringBuilder str = new StringBuilder();
        int i = 0;
        while (i < iMax && val.AbsNoSqrt() < 4)
        {
            i++;
            val.Pow(pow);
            val.Add(coord);
            str.Append(val.ToString() + ", ");
        }
        Debug.LogFormat("{0}, {1}, {2}: {3}", coord, i, di, str.ToString());
    }

    private double ToNormal(double m, int exp)
    {
        return m * System.Math.Pow(10, exp);
    }

    private float WeirdDivide(float top, float bottom)
    {
        float oneOver = 1 / bottom;
        return top * oneOver;
    }

    private void Save(Texture2D tex)
    {
        byte[] bytes = tex.EncodeToJPG();
        string path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
        string fileName = "fractal_" + julia_x + "_" + julia_y + "_" + System.DateTime.Now;
        Debug.Log(fileName.Replace("/", "-").Replace(":", "."));
        FileStream file = new FileStream(fileName.Replace("/", "-").Replace(":", ".") + ".jpg", FileMode.OpenOrCreate);
        file.Write(bytes, 0, bytes.Length);
        file.Close();
    }

    private void CheckInitRenderTexture()
    {
        if (tex == null || tex.width != width || tex.height != height)
            InitRenderTexture();
    }

    private void InitRenderTexture()
    {
        if (tex != null)
            tex.Release();
        tex = new RenderTexture(curWidth, curHeight, 0);
        tex.enableRandomWrite = true;
        tex.Create();
    }
}

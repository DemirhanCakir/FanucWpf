using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace FanucWpf
{
    public class FanucInterface
    {
        public string HostName;

        private readonly Random rnd = new Random();

        private FRRJIf.Core mobjCore;
        private FRRJIf.DataTable mobjDataTable;
        private FRRJIf.DataTable mobjDataTable2;

        // Data objects
        private FRRJIf.DataCurPos mobjCurPos;
        private FRRJIf.DataCurPos mobjCurPosUF;
        private FRRJIf.DataCurPos mobjCurPos2;
        private FRRJIf.DataTask mobjTask;
        private FRRJIf.DataTask mobjTaskIgnoreMacro;
        private FRRJIf.DataTask mobjTaskIgnoreKarel;
        private FRRJIf.DataTask mobjTaskIgnoreMacroKarel;
        private FRRJIf.DataPosReg mobjPosReg;
        private FRRJIf.DataPosReg mobjPosReg2;
        private FRRJIf.DataPosRegXyzwpr mobjPosRegXyzwpr;
        private FRRJIf.DataSysVar mobjSysVarInt;
        private FRRJIf.DataSysVar mobjSysVarInt2;
        private FRRJIf.DataSysVar mobjSysVarReal;
        private FRRJIf.DataSysVar mobjSysVarReal2;
        private FRRJIf.DataSysVar mobjSysVarString;
        private FRRJIf.DataSysVarPos mobjSysVarPos;
        private FRRJIf.DataSysVar[] mobjSysVarIntArray;
        private FRRJIf.DataNumReg mobjNumReg;
        private FRRJIf.DataNumReg mobjNumReg2;
        private FRRJIf.DataNumReg mobjNumReg3;
        private FRRJIf.DataAlarm mobjAlarm;
        private FRRJIf.DataAlarm mobjAlarmCurrent;
        private FRRJIf.DataSysVar mobjVarString;
        private FRRJIf.DataString mobjStrReg;
        private FRRJIf.DataString mobjStrRegComment;

        // Toggle sayaçları (UI yok; metot içinden durum tutmak için)
        private int _cntSetGO;
        private int _cntSetGI;
        private int _cntSetRDI;
        private int _cntSetRDO;
        private int _cntSetSDI;
        private int _cntSetSDO;

        public FanucInterface(string hostName)
        {
            HostName = hostName;    
        }

        private static long GetTickMs() => DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        public bool InitAndConnect(int timeoutMs = -1)
        {
            try
            {
                mobjCore = new FRRJIf.Core();

                // 1. DataTable oluştur (bağlantıdan önce)
                mobjDataTable = mobjCore.get_DataTable();

                // 1.a. Ana tablodaki abonelikler
                mobjAlarm = mobjDataTable.AddAlarm(FRRJIf.FRIF_DATA_TYPE.ALARM_LIST, 5, 0);
                mobjAlarmCurrent = mobjDataTable.AddAlarm(FRRJIf.FRIF_DATA_TYPE.ALARM_CURRENT, 1, 0);
                mobjCurPos = mobjDataTable.AddCurPos(FRRJIf.FRIF_DATA_TYPE.CURPOS, 1);
                mobjCurPosUF = mobjDataTable.AddCurPosUF(FRRJIf.FRIF_DATA_TYPE.CURPOS, 1, 15);
                mobjCurPos2 = mobjDataTable.AddCurPos(FRRJIf.FRIF_DATA_TYPE.CURPOS, 2);

                mobjTask = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK, 1);
                mobjTaskIgnoreMacro = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK_IGNORE_MACRO, 1);
                mobjTaskIgnoreKarel = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK_IGNORE_KAREL, 1);
                mobjTaskIgnoreMacroKarel = mobjDataTable.AddTask(FRRJIf.FRIF_DATA_TYPE.TASK_IGNORE_MACRO_KAREL, 1);

                mobjPosReg = mobjDataTable.AddPosReg(FRRJIf.FRIF_DATA_TYPE.POSREG, 1, 1, 10);
                mobjPosReg2 = mobjDataTable.AddPosReg(FRRJIf.FRIF_DATA_TYPE.POSREG, 2, 1, 4);
                mobjPosRegXyzwpr = mobjDataTable.AddPosRegXyzwpr(FRRJIf.FRIF_DATA_TYPE.POSREG_XYZWPR, 1, 1, 10);

                mobjSysVarInt = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$FAST_CLOCK");
                mobjSysVarInt2 = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, "$TIMER[10].$TIMER_VAL");
                mobjSysVarReal = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_REAL, "$MOR_GRP[1].$CURRENT_ANG[1]");
                mobjSysVarReal2 = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_REAL, "$DUTY_TEMP");
                mobjSysVarString = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_STRING, "$TIMER[10].$COMMENT");
                mobjSysVarPos = mobjDataTable.AddSysVarPos(FRRJIf.FRIF_DATA_TYPE.SYSVAR_POS, "$MNUTOOL[1,1]");
                mobjVarString = mobjDataTable.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_STRING, "$[HTTPKCL]CMDS[1]");
                mobjNumReg = mobjDataTable.AddNumReg(FRRJIf.FRIF_DATA_TYPE.NUMREG_INT, 1, 5);
                mobjNumReg2 = mobjDataTable.AddNumReg(FRRJIf.FRIF_DATA_TYPE.NUMREG_REAL, 6, 10);
                mobjStrReg = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.STRREG, 1, 3);
                mobjStrRegComment = mobjDataTable.AddString(FRRJIf.FRIF_DATA_TYPE.STRREG_COMMENT, 1, 3);

                // 2. İkinci DataTable (bağımsız)
                mobjDataTable2 = mobjCore.get_DataTable2();
                mobjNumReg3 = mobjDataTable2.AddNumReg(FRRJIf.FRIF_DATA_TYPE.NUMREG_INT, 1, 5);
                mobjSysVarIntArray = new FRRJIf.DataSysVar[10];
                for (int i = 0; i < 10; i++)
                {
                    mobjSysVarIntArray[i] =
                        mobjDataTable2.AddSysVar(FRRJIf.FRIF_DATA_TYPE.SYSVAR_INT, $"$TIMER[{i + 1}].$TIMER_VAL");
                }

                // 3. Bağlantı
                if (timeoutMs > 0)
                    mobjCore.set_TimeOutValue(timeoutMs);

                bool ok = mobjCore.Connect(HostName);
                return ok;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InitAndConnect error: " + ex.Message);
                return false;
            }
        }

        public string RefreshData()
        {
            if (mobjCore == null) return "Not connected.";

            var sb = new StringBuilder();
            double t0 = GetTickMs();

            // DataTable refresh
            if (!mobjDataTable.Refresh()) return "Disconnected or refresh error.";

            // IO okuma buffer'ları
            Array intSDO = new short[100];
            Array intSDO2 = new short[100];
            Array intSDO3 = new short[100];
            Array intSDI = new short[10];
            Array intRDO = new short[10];
            Array intRDI = new short[10];
            Array intSO = new short[10];
            Array intSI = new short[10];
            Array intUO = new short[10];
            Array intUI = new short[10];
            Array lngAO = new int[3];
            Array lngAI = new int[3];
            Array lngGO = new int[3];
            Array lngGO2 = new int[3];
            Array lngGI = new int[3];
            Array intWO = new short[5];
            Array intWI = new short[5];
            Array intWSI = new short[5];

            bool blnSDO = mobjCore.ReadSDO(1, ref intSDO, 100);
            bool blnSDO2 = mobjCore.ReadSDO(10001, ref intSDO2, 100);
            bool blnSDO3 = mobjCore.ReadSDO(11001, ref intSDO3, 100);
            bool blnSDI = mobjCore.ReadSDI(1, ref intSDI, 10);
            bool blnRDO = mobjCore.ReadRDO(1, ref intRDO, 8);
            bool blnRDI = mobjCore.ReadRDI(1, ref intRDI, 8);
            bool blnSO = mobjCore.ReadSO(0, ref intSO, 9);
            bool blnSI = mobjCore.ReadSI(0, ref intSI, 9);
            bool blnUO = mobjCore.ReadUO(1, ref intUO, 10);
            bool blnUI = mobjCore.ReadUI(1, ref intUI, 10);
            bool blnGO = mobjCore.ReadGO(1, ref lngGO, 3);
            bool blnGO2 = mobjCore.ReadGO(10001, ref lngGO2, 3);
            bool blnGI = mobjCore.ReadGI(1, ref lngGI, 3);

            // AO/AI offsets
            if (!mobjCore.ReadGO(1000 + 1, ref lngAO, 3)) return "Read AO error.";
            if (!mobjCore.ReadGI(1000 + 1, ref lngAI, 2)) return "Read AI error.";

            // WO/WI/WSI offsets
            if (!mobjCore.ReadSDO(8001, ref intWO, 5)) return "Read WO error.";
            if (!mobjCore.ReadSDI(8001, ref intWI, 5)) return "Read WI error.";
            if (!mobjCore.ReadSDI(8401, ref intWSI, 1)) return "Read WSI error.";

            double elapsed = GetTickMs() - t0;
            sb.AppendLine($"Time = {Convert.ToInt32(elapsed)}(msec)");

            // CurPos
            AppendCurPos(sb, "--- CurPos GP1 World ---", mobjCurPos);
            AppendCurPos(sb, "--- CurPos GP1 Current UF ---", mobjCurPosUF);
            AppendCurPos(sb, "--- CurPos GP2 World ---", mobjCurPos2);

            // Tasks
            AppendTask(sb, "--- Task ---", mobjTask);
            AppendTask(sb, "--- Task Ignore Macro ---", mobjTaskIgnoreMacro);
            AppendTask(sb, "--- Task Ignore KAREL ---", mobjTaskIgnoreKarel);
            AppendTask(sb, "--- Task Ignore Macro, KAREL ---", mobjTaskIgnoreMacroKarel);

            // SysVars
            sb.AppendLine("--- SysVar ---");
            object vntValue = null;
            AppendSysVar(sb, mobjSysVarInt, ref vntValue);
            AppendSysVar(sb, mobjSysVarInt2, ref vntValue);
            AppendSysVar(sb, mobjSysVarReal, ref vntValue);
            AppendSysVar(sb, mobjSysVarReal2, ref vntValue);
            AppendSysVar(sb, mobjSysVarString, ref vntValue);
            AppendSysVarPos(sb, mobjSysVarPos);

            // Free variable example
            AppendSysVar(sb, mobjVarString, ref vntValue);

            // NumReg
            sb.AppendLine("--- NumReg ---");
            AppendNumRegs(sb, mobjNumReg);
            AppendNumRegs(sb, mobjNumReg2);

            // PosReg
            sb.AppendLine("--- PosReg GP1 ---");
            AppendPosRegs(sb, mobjPosReg);
            sb.AppendLine("--- PosReg GP2 ---");
            AppendPosRegs(sb, mobjPosReg2, labelPrefix: "PR[GP2:");

            // IO dumps
            sb.AppendLine("--- SDO ---");
            sb.AppendLine(blnSDO ? mstrIO("SDO", 1, 100, ref intSDO) : "Error");
            sb.AppendLine("--- SDO[1000x] ---");
            sb.AppendLine(blnSDO2 ? mstrIO("SDO", 10001, 10100, ref intSDO2) : "Error");
            sb.AppendLine("--- SDO[1100x] ---");
            sb.AppendLine(blnSDO3 ? mstrIO("SDO", 11001, 11100, ref intSDO3) : "Error");
            sb.AppendLine("--- SDI ---");
            sb.AppendLine(blnSDI ? mstrIO("SDI", 1, 10, ref intSDI) : "Error");
            sb.AppendLine("--- RDO ---");
            sb.AppendLine(blnRDO ? mstrIO("RDO", 1, 8, ref intRDO) : "Error");
            sb.AppendLine("--- RDI ---");
            sb.AppendLine(blnRDI ? mstrIO("RDI", 1, 8, ref intRDI) : "Error");
            sb.AppendLine("--- SO ---");
            sb.AppendLine(blnSO ? mstrIO("SO", 0, 9, ref intSO) : "Error");
            sb.AppendLine("--- SI ---");
            sb.AppendLine(blnSI ? mstrIO("SI", 0, 9, ref intSI) : "Error");
            sb.AppendLine("--- UO ---");
            sb.AppendLine(blnUO ? mstrIO("UO", 1, 10, ref intUO) : "Error");
            sb.AppendLine("--- UI ---");
            sb.AppendLine(blnUI ? mstrIO("UI", 1, 10, ref intUI) : "Error");
            sb.AppendLine("--- GO ---");
            sb.AppendLine(blnGO ? mstrIO2("GO", 1, 3, ref lngGO) : "Error");
            sb.AppendLine("--- GO[1000x] ---");
            sb.AppendLine(blnGO2 ? mstrIO2("GO", 10001, 10003, ref lngGO2) : "Error");
            sb.AppendLine("--- GI ---");
            sb.AppendLine(blnGI ? mstrIO2("GI", 1, 3, ref lngGI) : "Error");
            sb.AppendLine("--- AO ---");
            sb.AppendLine(mstrIO2("AO", 1, 3, ref lngAO));
            sb.AppendLine("--- AI ---");
            sb.AppendLine(mstrIO2("AI", 1, 3, ref lngAI));
            sb.AppendLine("--- WO ---");
            sb.AppendLine(mstrIO("WO", 1, 5, ref intWO));
            sb.AppendLine("--- WI ---");
            sb.AppendLine(mstrIO("WI", 1, 5, ref intWI));
            sb.AppendLine("--- WSI ---");
            sb.AppendLine(mstrIO("WSI", 1, 1, ref intWSI));

            // Alarms
            for (int ii = 1; ii <= 5; ii++) sb.Append(mstrAlarm(ref mobjAlarm, ii));
            for (int ii = 1; ii <= 1; ii++) sb.Append(mstrAlarm(ref mobjAlarmCurrent, ii));

            // String registers (with comments)
            sb.AppendLine("--- StrReg ---");
            string strComment = "";
            string strValue = "";
            for (int ii = mobjStrReg.StartIndex; ii <= mobjStrReg.EndIndex; ii++)
            {
                mobjStrRegComment.GetValue(ii, ref strComment);
                if (mobjStrReg.GetValue(ii, ref strValue))
                    sb.AppendLine($"SR[{ii}:{strComment}] = {strValue}");
                else
                    sb.AppendLine($"SR[{ii}]  : Error!!!");
            }

            return sb.ToString();
        }

        public string RefreshDataTable2()
        {
            if (mobjCore == null) return "Not connected.";

            var sb = new StringBuilder();
            double t0 = GetTickMs();

            // Örnek yazma (NumReg3) ve ardından refresh
            int intRand = rnd.Next(1, 10);
            int[] intValues = new int[101];

            for (int ii = 0; ii <= mobjNumReg3.EndIndex - mobjNumReg3.StartIndex; ii++)
            {
                intValues[ii] = (ii + 1) * intRand;
            }
            if (!mobjNumReg3.SetValues(mobjNumReg3.StartIndex, intValues, mobjNumReg3.EndIndex - mobjNumReg3.StartIndex + 1))
                sb.AppendLine("SetNumReg Int Error");

            if (!mobjDataTable2.Refresh()) return "Disconnected or refresh error (DT2).";

            double elapsed = GetTickMs() - t0;
            sb.AppendLine($"Time = {Convert.ToInt32(elapsed)}(msec)");

            // NumReg3
            sb.AppendLine("--- NumReg ---");
            object vntValue = null;
            for (int ii = mobjNumReg3.StartIndex; ii <= mobjNumReg3.EndIndex; ii++)
            {
                if (mobjNumReg3.GetValue(ii, ref vntValue))
                    sb.AppendLine($"R[{ii}] = {vntValue}");
                else
                    sb.AppendLine($"R[{ii}] : Error!!!");
            }

            // SysVar dizi
            sb.AppendLine("--- SysVar ---");
            for (int ii = mobjSysVarIntArray.GetLowerBound(0); ii <= mobjSysVarIntArray.GetUpperBound(0); ii++)
            {
                if (mobjSysVarIntArray[ii].GetValue(ref vntValue))
                    sb.AppendLine($"{mobjSysVarIntArray[ii].SysVarName} = {vntValue}");
                else
                    sb.AppendLine($"{mobjSysVarIntArray[ii].SysVarName} : Error!!!");
            }

            return sb.ToString();
        }

        // Set/Get: Numeric registers
        public void SetNumRegs()
        {
            int intRand = rnd.Next(1, 10);

            // INT R[1..5]
            int[] intValues = new int[mobjNumReg.EndIndex - mobjNumReg.StartIndex + 1];
            for (int i = 0; i < intValues.Length; i++)
                intValues[i] = (i + 1) * intRand;

            if (!mobjNumReg.SetValues(mobjNumReg.StartIndex, intValues, intValues.Length))
                throw new Exception("SetNumReg Int Error");

            // REAL R[6..10]
            float[] sngValues = new float[mobjNumReg2.EndIndex - mobjNumReg2.StartIndex + 1];
            for (int i = 0; i < sngValues.Length; i++)
                sngValues[i] = (float)((i + 1) * intRand * 1.1);

            if (!mobjNumReg2.SetValues(mobjNumReg2.StartIndex, sngValues, sngValues.Length))
                throw new Exception("SetNumReg Real Error");
        }

        // Set: String registers (SR)
        public void SetStringRegs()
        {
            int intRand = rnd.Next(1, 10);
            bool ok;

            for (int ii = mobjStrReg.StartIndex; ii <= mobjStrReg.EndIndex; ii++)
            {
                string value = $"str{ii + intRand}";
                ok = mobjStrReg.SetValue(ii, value);
                Debug.Assert(ok);
            }
            ok = mobjStrReg.Update(); // toplu gönderim
            Debug.Assert(ok);
        }

        // Set: Position registers by Joint
        public void SetPosRegsJoint()
        {
            Array sngJoint = new float[6];
            int intRand = rnd.Next(1, 10);

            for (int ii = mobjPosReg.StartIndex; ii <= mobjPosReg.EndIndex; ii++)
            {
                for (int jj = sngJoint.GetLowerBound(0); jj <= sngJoint.GetUpperBound(0); jj++)
                {
                    sngJoint.SetValue((float)(11.11 * (jj + 1) * intRand * ii), jj);
                }
                mobjPosReg.SetValueJoint(ii, ref sngJoint, 15, 15);
            }

            // GP2 örnek (sadece J1)
            for (int ii = mobjPosReg2.StartIndex; ii <= mobjPosReg2.EndIndex; ii++)
            {
                int jj = 0;
                sngJoint.SetValue((float)(11.11 * (jj + 1) * intRand * ii), jj);
                mobjPosReg2.SetValueJoint(ii, ref sngJoint, 15, 15);
            }
        }

        // Set: Position registers by XYZWPR
        public void SetPosRegsXyzwpr()
        {
            Array sngArray = new float[9];
            Array intConfig = new short[7];
            int intRand = rnd.Next(1, 10);

            for (int ii = mobjPosReg.StartIndex; ii <= mobjPosReg.EndIndex; ii++)
            {
                for (int jj = sngArray.GetLowerBound(0); jj <= sngArray.GetUpperBound(0); jj++)
                    sngArray.SetValue((float)(11.11 * (jj + 1) * intRand * ii), jj);

                intConfig.SetValue((short)ii, 4);
                intConfig.SetValue((short)ii, 5);
                intConfig.SetValue((short)ii, 6);

                mobjPosReg.SetValueXyzwpr(ii, ref sngArray, ref intConfig, -1, -1);
            }
        }

        // Set: Position registers via POSREG_XYZWPR (batched)
        public void SetPosRegsXyzwprBatched()
        {
            Array sngArray = new float[6];
            Array intConfig = new short[7];
            int intRand = rnd.Next(1, 10);

            for (int ii = mobjPosRegXyzwpr.StartIndex; ii <= mobjPosRegXyzwpr.EndIndex; ii++)
            {
                for (int jj = sngArray.GetLowerBound(0); jj <= sngArray.GetUpperBound(0); jj++)
                    sngArray.SetValue((float)(11.11 * (jj + 1) * intRand * ii), jj);

                intConfig.SetValue((short)ii, 4);
                intConfig.SetValue((short)ii, 5);
                intConfig.SetValue((short)ii, 6);

                bool ok = mobjPosRegXyzwpr.SetValueXyzwpr(ii, ref sngArray, ref intConfig);
                if (!ok) throw new Exception("mobjPosRegXyzwpr.SetValueXyzwpr error");
            }
            if (!mobjPosRegXyzwpr.Update())
                throw new Exception("mobjPosRegXyzwpr.Update error");
        }

        // IO Writes

        // SDO (Standard Digital Output)
        public void WriteSDO(short startIndex) // 1, 10001 veya 11001
        {
            Array intVal = new short[100];
            _cntSetSDO++;
            if (_cntSetSDO % 2 == 1)
            {
                for (int i = 0; i < 100; i++) intVal.SetValue((short)1, i);
            }
            if (!mobjCore.WriteSDO(startIndex, ref intVal, 100))
                throw new Exception("WriteSDO error");
        }

        // SDI (Standard Digital Input)
        public void WriteSDI()
        {
            Array intVal = new short[10];
            _cntSetSDI++;
            if (_cntSetSDI % 2 == 1)
            {
                for (int i = 0; i < 10; i++) intVal.SetValue((short)1, i);
            }
            if (!mobjCore.WriteSDI(1, ref intVal, 10))
                throw new Exception("WriteSDI error");
        }

        // RDO (Robot Digital Output)
        public void WriteRDO()
        {
            Array intVal = new short[10];
            _cntSetRDO++;
            if (_cntSetRDO % 2 == 1)
            {
                for (int i = 0; i <= 7; i++) intVal.SetValue((short)1, i);
            }
            if (!mobjCore.WriteRDO(1, ref intVal, 8))
                throw new Exception("WriteRDO error");
        }

        // RDI (Robot Digital Input)
        public void WriteRDI()
        {
            short[] buf = new short[10];
            _cntSetRDI++;
            if (_cntSetRDI % 2 == 1)
            {
                for (int i = 0; i <= 7; i++) buf[i] = 1;
            }
            Array tmp = buf;
            if (!mobjCore.WriteRDI(1, ref tmp, 8))
                throw new Exception("WriteRDI error");
        }

        // GO (Group Output) — startIndex: 1 ya da 10001
        public void WriteGO(short startIndex)
        {
            Array lngVal = new int[3];
            _cntSetGO++;
            for (int i = 0; i <= 2; i++)
                lngVal.SetValue(_cntSetGO * (i + 1), i);

            if (!mobjCore.WriteGO(startIndex, ref lngVal, 3))
                throw new Exception("WriteGO error");
        }

        // GI (Group Input)
        public void WriteGI()
        {
            Array lngVal = new int[3];
            _cntSetGI++;
            for (int i = 0; i <= 2; i++)
                lngVal.SetValue(_cntSetGI * (i + 1), i);

            if (!mobjCore.WriteGI(1, ref lngVal, 3))
                throw new Exception("WriteGI error");
        }

        // SysVar write/read test (örnek)
        public void WriteSysVarsTest()
        {
            // Eski değerleri al
            if (!mobjDataTable.Refresh()) throw new Exception("Refresh failed.");

            object objTmp = null;
            mobjSysVarInt2.GetValue(ref objTmp);
            int lngOld = (int)objTmp;

            mobjSysVarString.GetValue(ref objTmp);
            string strOld = (string)objTmp;

            mobjSysVarReal2.GetValue(ref objTmp);
            float sngOld = (float)objTmp;

            Array xyzwpr = new float[9];
            Array config = new short[7];
            Array joint = new float[9];
            short intUF = 0, intUT = 0, intValidC = 0, intValidJ = 0;

            mobjSysVarPos.GetValue(ref xyzwpr, ref config, ref joint,
                                   ref intUF, ref intUT, ref intValidC, ref intValidJ);
            float sngXOld = (float)xyzwpr.GetValue(0);

            // Yeni değerler
            int lngNew = 999;
            float sngNew = sngOld + 1;
            string strNew = "abc";
            float sngXNew = sngXOld + 1;
            xyzwpr.SetValue(sngXNew, 0);

            // Yaz
            mobjSysVarInt2.SetValue(lngNew);
            mobjSysVarString.SetValue(strNew);
            mobjSysVarReal2.SetValue(sngNew);
            mobjSysVarPos.SetValueXyzwpr(ref xyzwpr, ref config, intUF, intUT);

            // Onayla
            mobjDataTable.Refresh();
            mobjSysVarInt2.GetValue(ref objTmp);
            Debug.Assert(lngNew == (int)objTmp);
            mobjSysVarString.GetValue(ref objTmp);
            Debug.Assert(strNew == (string)objTmp);
            mobjSysVarReal2.GetValue(ref objTmp);
            Debug.Assert(Math.Abs(sngNew - (float)objTmp) < 1e-6);
            mobjSysVarPos.GetValue(ref xyzwpr, ref config, ref joint,
                                   ref intUF, ref intUT, ref intValidC, ref intValidJ);
            Debug.Assert(Math.Abs(sngXNew - (float)xyzwpr.GetValue(0)) < 1e-6);

            // Eskiyi geri yükle
            mobjSysVarInt2.SetValue(lngOld);
            mobjSysVarString.SetValue(strOld);
            mobjSysVarReal2.SetValue(sngOld);
            xyzwpr.SetValue(sngXOld, 0);
            mobjSysVarPos.SetValueXyzwpr(ref xyzwpr, ref config, intUF, intUT);

            // Son teyit
            mobjDataTable.Refresh();
            mobjSysVarInt2.GetValue(ref objTmp);
            Debug.Assert(lngOld == (int)objTmp);
            mobjSysVarString.GetValue(ref objTmp);
            Debug.Assert(strOld == (string)objTmp);
            mobjSysVarReal2.GetValue(ref objTmp);
            Debug.Assert(Math.Abs(sngOld - (float)objTmp) < 1e-6);
            mobjSysVarPos.GetValue(ref xyzwpr, ref config, ref joint,
                                   ref intUF, ref intUT, ref intValidC, ref intValidJ);
            Debug.Assert(Math.Abs(sngXOld - (float)xyzwpr.GetValue(0)) < 1e-6);
        }

        public void Disconnect()
        {
            try
            {
                if (mobjCore != null)
                    mobjCore.Disconnect();
            }
            catch { /* ignore */ }
        }

        // Helpers

        private void AppendCurPos(StringBuilder sb, string title, FRRJIf.DataCurPos curPos)
        {
            Array xyzwpr = new float[9];
            Array config = new short[7];
            Array joint = new float[9];
            short intUF = 0, intUT = 0, intValidC = 0, intValidJ = 0;

            if (curPos.GetValue(ref xyzwpr, ref config, ref joint, ref intUF, ref intUT, ref intValidC, ref intValidJ))
            {
                sb.AppendLine(title);
                sb.Append(mstrPos(ref xyzwpr, ref config, ref joint, intValidC, intValidJ, intUF, intUT));
            }
            else
            {
                sb.AppendLine($"{title} Error!!!");
            }
        }

        private void AppendTask(StringBuilder sb, string title, FRRJIf.DataTask task)
        {
            string strProg = "";
            short intLine = 0, intState = 0;
            string strParentProg = "";

            if (task.GetValue(ref strProg, ref intLine, ref intState, ref strParentProg))
            {
                sb.AppendLine(title);
                sb.Append(mstrTask(task.Index, strProg, intLine, intState, strParentProg));
            }
            else
            {
                sb.AppendLine("Task Error!!!");
            }
        }

        private void AppendSysVar(StringBuilder sb, FRRJIf.DataSysVar var, ref object v)
        {
            if (var.GetValue(ref v))
                sb.AppendLine($"{var.SysVarName} = {v}");
            else
                sb.AppendLine($"{var.SysVarName} : Error!!!");
        }

        private void AppendSysVarPos(StringBuilder sb, FRRJIf.DataSysVarPos var)
        {
            Array xyzwpr = new float[9];
            Array config = new short[7];
            Array joint = new float[9];
            short intUF = 0, intUT = 0, intValidC = 0, intValidJ = 0;

            if (var.GetValue(ref xyzwpr, ref config, ref joint, ref intUF, ref intUT, ref intValidC, ref intValidJ))
            {
                sb.AppendLine(var.SysVarName);
                sb.Append(mstrPos(ref xyzwpr, ref config, ref joint, intValidC, intValidJ, intUF, intUT));
            }
            else
            {
                sb.AppendLine($"{var.SysVarName} : Error!!!");
            }
        }

        private void AppendNumRegs(StringBuilder sb, FRRJIf.DataNumReg nr)
        {
            object v = null;
            for (int ii = nr.StartIndex; ii <= nr.EndIndex; ii++)
            {
                if (nr.GetValue(ii, ref v))
                    sb.AppendLine($"R[{ii}] = {v}");
                else
                    sb.AppendLine($"R[{ii}] : Error!!!");
            }
        }

        private void AppendPosRegs(StringBuilder sb, FRRJIf.DataPosReg pr, string labelPrefix = "PR[")
        {
            Array xyzwpr = new float[9];
            Array config = new short[7];
            Array joint = new float[9];
            short intUF = 0, intUT = 0, intValidC = 0, intValidJ = 0;

            for (int ii = pr.StartIndex; ii <= pr.EndIndex; ii++)
            {
                if (pr.GetValue(ii, ref xyzwpr, ref config, ref joint, ref intUF, ref intUT, ref intValidC, ref intValidJ))
                {
                    sb.AppendLine($"{labelPrefix}{ii}]");
                    sb.Append(mstrPos(ref xyzwpr, ref config, ref joint, intValidC, intValidJ, intUF, intUT));
                }
                else
                {
                    sb.AppendLine($"{labelPrefix}{ii}] : Error!!!");
                }
            }
        }

        private string mstrIO(string strIOType, short StartIndex, short EndIndex, ref Array values)
        {
            var sb = new StringBuilder();
            sb.Append($"{strIOType}[{StartIndex}-{EndIndex}]=");
            for (int ii = 0; ii <= EndIndex - StartIndex; ii++)
                sb.Append(((short)values.GetValue(ii) == 0) ? "0" : "1");
            return sb.ToString();
        }

        private string mstrIO2(string strIOType, short StartIndex, short EndIndex, ref Array values)
        {
            var sb = new StringBuilder();
            sb.Append($"{strIOType}[{StartIndex}-{EndIndex}]=");
            for (int ii = 0; ii <= EndIndex - StartIndex; ii++)
            {
                if (ii != 0) sb.Append(",");
                sb.Append(values.GetValue(ii));
            }
            return sb.ToString();
        }

        private string mstrPos(ref Array xyzwpr, ref Array config, ref Array joint,
                               short intValidC, short intValidJ, int UF, int UT)
        {
            var sb = new StringBuilder();
            sb.Append("UF = ").Append(UF).Append(", UT = ").Append(UT).AppendLine();

            if (intValidC != 0)
            {
                sb.Append("XYZWPR = ");
                for (int ii = 0; ii <= 8; ii++) sb.Append(xyzwpr.GetValue(ii)).Append(" ");
                sb.AppendLine();

                sb.Append("CONFIG = ");
                sb.Append(((short)config.GetValue(0) != 0) ? "F " : "N ");
                sb.Append(((short)config.GetValue(1) != 0) ? "L " : "R ");
                sb.Append(((short)config.GetValue(2) != 0) ? "U " : "D ");
                sb.Append(((short)config.GetValue(3) != 0) ? "T " : "B ");
                sb.AppendFormat("{0}, {1}, {2}", config.GetValue(4), config.GetValue(5), config.GetValue(6)).AppendLine();
            }

            if (intValidJ != 0)
            {
                sb.Append("JOINT = ");
                for (int ii = 0; ii <= 8; ii++) sb.Append(joint.GetValue(ii)).Append(" ");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string mstrTask(int Index, string strProg, short intLine, short intState, string strParentProg)
        {
            return $"TASK{Index} :  Prog=\"{strProg}\" Line={intLine} State={intState} ParentProg=\"{strParentProg}\"\r\n";
        }

        private string mstrAlarm(ref FRRJIf.DataAlarm objAlarm, int Count)
        {
            short intID = 0, intNumber = 0, intCID = 0, intCNumber = 0, intSeverity = 0;
            short intY = 0, intM = 0, intD = 0, intH = 0, intMn = 0, intS = 0;
            string strM1 = "", strM2 = "", strM3 = "";
            bool blnRes = objAlarm.GetValue(Count, ref intID, ref intNumber, ref intCID, ref intCNumber, ref intSeverity,
                                            ref intY, ref intM, ref intD, ref intH, ref intMn, ref intS, ref strM1, ref strM2, ref strM3);
            var sb = new StringBuilder();
            sb.AppendLine($"-- Alarm {Count} --");
            if (!blnRes)
            {
                sb.AppendLine("Error");
                return sb.ToString();
            }
            sb.AppendLine($"{intID}, {intNumber}, {intCID}, {intCNumber}, {intSeverity}");
            sb.AppendLine($"{intY}/{intM}/{intD}, {intH}:{intMn}:{intS}");
            if (!string.IsNullOrEmpty(strM1)) sb.AppendLine(strM1);
            if (!string.IsNullOrEmpty(strM2)) sb.AppendLine(strM2);
            if (!string.IsNullOrEmpty(strM3)) sb.AppendLine(strM3);
            return sb.ToString();
        }

        public void Dispose()
        {
            try { Disconnect(); } catch { /* ignore */ }
        }
    }
}
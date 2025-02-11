using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Samba.Infrastructure.Settings;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Win32;

namespace Samba.Domain.Models.Settings
{
    public static class BusinessInfo  //Added by Tim GU
    {
        public static string SRSKey;
        public static string BusinessName;
        public static string TPSNumber;
        public static string TVQNumber;
        public static string CiviqNumber;
        public static string Street;
        public static string City;
        public static string Province = "QC";
        public static string PostCode;
        public static string PhoneNumber;
        static BusinessInfo() 
        {
             string strSrsKey = string.Empty;
            try
            {
                using (RegistryKey rke = Registry.CurrentUser.CreateSubKey(@"Software\Allagma\SRS\State"))
                {
                    string rkeV = rke.GetValue("Active_SRS_KEY", "").ToString();
                    strSrsKey = rkeV;
                    rke.Close();
                }
            }
            catch (Exception)
            {
            }

            string strCon = LocalSettings.ConnectionString;
            if (!string.IsNullOrEmpty(strCon))
            {
                using (SqlConnection con = new SqlConnection(strCon))
                {
                    try
                    {
                        string strSql = strSrsKey == string.Empty ? "SELECT * FROM BusinessInfo" : $"SELECT * FROM BusinessInfo WHERE SRS_KEY = '{strSrsKey}'";
                        SqlCommand cmd = new SqlCommand(strSql, con);
                        con.Open();
                        SqlDataReader reader = cmd.ExecuteReader();

                        if (reader.Read())
                        {
                            SRSKey = reader.GetString(reader.GetOrdinal("SRS_KEY"));
                            BusinessName = reader.GetString(reader.GetOrdinal("BUSINESS_NAME"));
                            TVQNumber = reader.GetString(reader.GetOrdinal("TVQ_NUMBER"));
                            TPSNumber = reader.GetString(reader.GetOrdinal("TPS_NUMBER"));
                            CiviqNumber = reader.GetString(reader.GetOrdinal("CIVIC_NUMBER"));
                            Street = reader.GetString(reader.GetOrdinal("STREET"));
                            City = reader.GetString(reader.GetOrdinal("CITY"));
                            Province = reader.GetString(reader.GetOrdinal("PROVINCE"));
                            PostCode = reader.GetString(reader.GetOrdinal("POST_CODE"));
                            PhoneNumber = reader.GetString(reader.GetOrdinal("PHONE_NUMBER"));
                        }
                    }
                    catch (Exception)
                    {

                    }
                    finally
                    {
                        if (con.State == ConnectionState.Open)
                            con.Close();
                    }

                }
            }
        }       
    }
}

using System;
using System.Configuration;

namespace SM64O
{
    public static class Constants
    {
        static Constants()
        {
            try
            {
                Characters = ConfigurationManager.AppSettings["characters"].Split(',');
            }
            catch (Exception ex)
            {
                // Config not found or invalid, revert to default
                if (ex is NullReferenceException || ex is ConfigurationErrorsException)
                {
                    Characters = new string[]
                    {
                        "Mario",
                        "Luigi",
                        "Yoshi",
                        "Wario",
                        "Peach",
                        "Toad",
                        "Waluigi",
                        "Rosalina"
                    };
                }
                else throw;
            }

            try
            {
                Gamemodes = ConfigurationManager.AppSettings["gamemodes"].Split(',');
            }
            catch (Exception ex)
            {
                // Config not found or invalid, revert to default
                if (ex is NullReferenceException || ex is ConfigurationErrorsException)
                {
                    Gamemodes = new string[]
                    {
                        "Normal Mode",
                        "3rd Person Shooter",
                        "No Interactions",
                        "Prop Hunt",
                        "Boss Rush",
                        "Tag",
                        "Hide 'n' Seek",
                    };
                }
                else throw;
            }
        }

        public static readonly string[] Characters;

        public static readonly string[] Gamemodes;
    }
}
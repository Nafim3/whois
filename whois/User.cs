using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace whois
{
    #region User class starts:
    public class User
    {

        
        public User(){}
        
        public String? UserID { get; set; }
        public String? Surname { get; set; }
        public String? Fornames { get; set; }
        public String? Title { get; set; }
        public String? Position { get; set; }
        public String? Phone { get; set; }
        public String? Email { get; set; }
        public String? Location { get; set; }
        public User(String userID,String surname,String fornames, String title,String position,String phone,String email,String location)
   
        {
            UserID = userID;
            Surname = surname;
            Fornames = fornames;
            Title = title;
            Position = position;
            Phone = phone;
            Email = email;
            Location = location;

        }
    };
    #endregion
}

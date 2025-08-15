using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResAuthApi.Application.DTOs
{
    public record RefreshResponse(string access_token, int expires_in);
}

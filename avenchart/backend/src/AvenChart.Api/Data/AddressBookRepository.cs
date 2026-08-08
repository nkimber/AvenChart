// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

using Npgsql;
using NpgsqlTypes;
using AvenChart.Api.Models;
namespace AvenChart.Api.Data;

public sealed class AddressBookRepository(NpgsqlDataSource dataSource)
{
    public async Task<AddressBookResponse> SearchAsync(string? organization, string? firstName, string? lastName, string? specialty, string? npi, string? type, bool externalOnly, CancellationToken ct)
    {
        await using var c = await dataSource.OpenConnectionAsync(ct); await using var cmd = c.CreateCommand();
        cmd.CommandText = """
          with entries as (
            select -s.id as id, true as internal, s.username, ''::text organization, s.first_name, s.last_name, null::text specialty, s.npi, 'internal'::text type, null::text phone, null::text mobile, null::text fax, s.email, null::text street, null::text city, null::text state, null::text postal_code, s.active from staff s
            union all
            select id, false, null, organization, first_name, last_name, specialty, npi, contact_type, phone, mobile, fax, email, street, city, state, postal_code, active from address_book_contacts
          ) select *, count(*) over() total from entries where active and (@externalOnly = false or internal = false)
            and organization ilike @organization and first_name ilike @firstName and last_name ilike @lastName and coalesce(specialty,'') ilike @specialty and coalesce(npi,'') ilike @npi and (@type = '' or type = @type)
          order by organization, last_name, first_name limit 500;
          """;
        AddFilter(cmd,"organization",organization); AddFilter(cmd,"firstName",firstName); AddFilter(cmd,"lastName",lastName); AddFilter(cmd,"specialty",specialty); AddFilter(cmd,"npi",npi); cmd.Parameters.AddWithValue("type", type?.Trim() ?? ""); cmd.Parameters.AddWithValue("externalOnly",externalOnly);
        var entries=new List<AddressBookEntry>(); var total=0; await using var r=await cmd.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)){ total=Convert.ToInt32(r.GetInt64(18)); entries.Add(new(r.GetInt32(0),r.GetBoolean(1),Text(r,2),r.GetString(3),r.GetString(4),r.GetString(5),Text(r,6),Text(r,7),r.GetString(8),Text(r,9),Text(r,10),Text(r,11),Text(r,12),Text(r,13),Text(r,14),Text(r,15),Text(r,16),r.GetBoolean(17))); } return new(entries,total);
    }
    public async Task<AddressBookEntry?> SaveAsync(int? id, AddressBookContactRequest request, CancellationToken ct)
    { var org=Required(request.Organization,"Organization"); var first=Required(request.FirstName,"First name"); var last=Required(request.LastName,"Last name"); await using var c=await dataSource.OpenConnectionAsync(ct); await using var cmd=c.CreateCommand(); cmd.CommandText=id is null ? """insert into address_book_contacts (organization,first_name,last_name,specialty,npi,contact_type,phone,mobile,fax,email,street,city,state,postal_code,active) values (@organization,@first,@last,@specialty,@npi,@type,@phone,@mobile,@fax,@email,@street,@city,@state,@postal,@active) returning id;""" : """update address_book_contacts set organization=@organization,first_name=@first,last_name=@last,specialty=@specialty,npi=@npi,contact_type=@type,phone=@phone,mobile=@mobile,fax=@fax,email=@email,street=@street,city=@city,state=@state,postal_code=@postal,active=@active where id=@id returning id;"""; if(id is not null)cmd.Parameters.AddWithValue("id",id.Value); cmd.Parameters.AddWithValue("organization",org); cmd.Parameters.AddWithValue("first",first);cmd.Parameters.AddWithValue("last",last); Add(cmd,"specialty",request.Specialty);Add(cmd,"npi",request.Npi);cmd.Parameters.AddWithValue("type",request.Type?.Trim()??"external_provider");Add(cmd,"phone",request.Phone);Add(cmd,"mobile",request.Mobile);Add(cmd,"fax",request.Fax);Add(cmd,"email",request.Email);Add(cmd,"street",request.Street);Add(cmd,"city",request.City);Add(cmd,"state",request.State);Add(cmd,"postal",request.PostalCode);cmd.Parameters.AddWithValue("active",request.Active??true); var result=await cmd.ExecuteScalarAsync(ct); if(result is null)return null; var all=await SearchAsync(org,first,last,null,null,null,true,ct);return all.Entries.FirstOrDefault(x=>!x.IsInternal&&x.Id==(int)result); }
    public async Task<bool> DeleteAsync(int id,CancellationToken ct){await using var c=await dataSource.OpenConnectionAsync(ct);await using var cmd=c.CreateCommand();cmd.CommandText="delete from address_book_contacts where id=@id;";cmd.Parameters.AddWithValue("id",id);return await cmd.ExecuteNonQueryAsync(ct)==1;}
    static void AddFilter(NpgsqlCommand c,string key,string? value)=>c.Parameters.AddWithValue(key,"%"+(value?.Trim()??"")+"%"); static void Add(NpgsqlCommand c,string key,string? value)=>c.Parameters.Add(key,NpgsqlDbType.Text).Value=string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim(); static string? Text(NpgsqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i); static string Required(string? value,string name)=>string.IsNullOrWhiteSpace(value)||value.Trim().Length>120?throw new ArgumentException($"{name} is required and must be 120 characters or fewer."):value.Trim();
}

insert into form_layout_fields(layout_key,field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value,updated_at,updated_by) values
  ('DEM','state','contact','State or province','select',30,false,true,2,'state','',now(),'seed')
on conflict(layout_key,field_key) do nothing;

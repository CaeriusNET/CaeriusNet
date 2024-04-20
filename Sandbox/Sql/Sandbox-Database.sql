create type tvp_newUsers as table
(
    user_name nvarchar(60),
    user_pass nvarchar(60)
)
go

create type tvp_users as table
(
    user_guid uniqueidentifier,
    user_name nvarchar(60)
)
go

create type tvp_usersAge as table
(
    user_guid uniqueidentifier,
    user_age  int
)
go



create table Users
(
    id   int identity
        constraint Users_pk
            primary key,
    guid uniqueidentifier default newsequentialid() not null rowguidcol,
    [
       user]
         nvarchar(60)                               not null,
    pass nvarchar(60)                               not null,
    age  tinyint          default 0                 not null
)
go

create index Users_guid_pass_index
    on Users (guid, pass)
go

create index Users_guid_index
    on Users (guid)
go

create index Users_guid_age_index
    on Users (guid, age)
go



create procedure dbo.sp_create_user(
    @Username nvarchar(60),
    @Password nvarchar(60)
)
as
begin
    set nocount on
    set transaction isolation level read uncommitted

    begin try
        begin transaction
            insert into dbo.Users ([user], pass)
            values (@Username, @Password)
            if @@trancount > 0
                commit transaction
    end try
    begin catch
        if @@trancount > 0
            rollback transaction
    end catch
end
go

create procedure dbo.sp_create_user_with_tvp(
    @MyTvpUsers dbo.tvp_newUsers readonly
)
as
begin
    set nocount on
    set transaction isolation level read uncommitted

    begin try
        begin transaction
            insert into dbo.Users ([user], pass)
            select t.user_name, t.user_pass
            from @MyTvpUsers as t

            if @@trancount > 0
                commit transaction
    end try
    begin catch
        if @@trancount > 0
            rollback transaction
    end catch
end
go

CREATE procedure dbo.sp_get_users
as
begin
    set nocount on
    set transaction isolation level read uncommitted

    begin try
        begin transaction
            select guid, [user], pass, age
            from dbo.Users
            if @@trancount > 0
                commit transaction
    end try
    begin catch
        if @@trancount > 0
            rollback transaction
    end catch
end
go

CREATE procedure dbo.sp_update_user_age(
    @MyTvpUserAge dbo.tvp_usersAge readonly
)
as
begin
    set transaction isolation level read uncommitted

    begin try
        begin transaction
            update dbo.Users
            set age = u.age + tvp.user_age
            from dbo.Users as u
                     inner join @MyTvpUserAge as tvp
                                on u.guid = tvp.user_guid
            where u.guid = tvp.user_guid
            if @@trancount > 0
                commit transaction
    end try
    begin catch
        if @@trancount > 0
            rollback transaction
    end catch
end
go
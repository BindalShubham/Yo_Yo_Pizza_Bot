using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using Yo_Yo_Bot.Models;

namespace Yo_Yo_Bot.Data
{
    public class DataBaseOperations
    {
        SqlConnection connection;
        public DataBaseOperations(string connectionString)
        {
            connection = new SqlConnection(connectionString);
        }

        public DataTable GetAll()
        {
            DataTable dt = new DataTable();
            try
            {
                connection.Open();
              
                SqlDataAdapter da = new SqlDataAdapter("select * from pizzaorders", connection);
                da.Fill(dt);
                return dt;
            }
            catch(Exception ex)
            {
                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }

        public DataRow Get(int? id)
        {
            DataTable dt = new DataTable();
            try
            {
                connection.Open();

                SqlDataAdapter da = new SqlDataAdapter($"select * from pizzaorders where id={id}", connection);
                da.Fill(dt);
                return dt.Rows[0];
            }
            catch (Exception ex)
            {
                //throw ex;
                return null;
            }
            finally
            {
                connection.Close();
            }
        }

        public DataRow GetByName(string name)
        {
            DataTable dt = new DataTable();
            try
            {
                connection.Open();

                SqlDataAdapter da = new SqlDataAdapter($"select * from specialPizzas where pizzaname='{name}'", connection);
                da.Fill(dt);
                return dt.Rows[0];
            }
            catch (Exception ex)
            {
                //throw ex;
                return null;
            }
            finally
            {
                connection.Close();
            }
        }

        public void addUserInfo(OnboardingState onboardingState)
        {
            DataTable dt = new DataTable();
            try
            {
                connection.Open();
                SqlCommand cmd = new SqlCommand(
                    $"insert into UserInfo(username,phoneNumber)" +
                    $"values('{onboardingState.Name}','{onboardingState.Number}');",
                    connection);
                cmd.ExecuteNonQuery();
            }
            catch(Exception ex)
            {

            }
            finally
            {
                connection.Close();
            }
        }
        public int Add(PizzaOrderState pizzaOrderState, long? phoneNumber)
        {
            var number = Convert.ToInt64(phoneNumber);
            DataTable dt = new DataTable();
            try
            {
                connection.Open();
                SqlCommand cmd = new SqlCommand(
                    $"insert into pizzaorders(pizzaname,size,cheese,sauce,Address,time,vegTops,nonVegTops,base,price,phoneNumber)" +
                    $"values('{pizzaOrderState.PizzaName}','{pizzaOrderState.Size}','{pizzaOrderState.Cheese}','{pizzaOrderState.Sauce}','{pizzaOrderState.Location}','{pizzaOrderState.DeliveryTime}','{string.Join(", ",pizzaOrderState.VegTops)}','{string.Join(", ", pizzaOrderState.NonVegTops)}','{pizzaOrderState.Base}','{(int)pizzaOrderState.Price}','{number}'); SELECT SCOPE_IDENTITY()",
                    connection);
                return Convert.ToInt32(cmd.ExecuteScalar());
                //return 0;


            }
            catch (Exception ex)
            {
                return 0;
            }
            finally
            {
                connection.Close();
            }
        }

        public DataTable GetSpecialPizzas()
        {
            DataTable dt = new DataTable();
            try
            {
                SqlCommand cmd = new SqlCommand("select * from specialPizzas", connection);
                connection.Open();

                // create data adapter
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                // this will query your database and return the result to your datatable
                da.Fill(dt);
                da.Dispose();
                return dt;
            }
            catch (Exception ex)
            {
                //throw ex;
                return null;
            }
            finally
            {
                connection.Close();
            }
        }

        public void Update(string name,int id)
        {
            DataTable dt = new DataTable();
            try
            {
                connection.Open();
                SqlCommand cmd = new SqlCommand($"update  test set name = {name} where id ={id}", connection);
                cmd.ExecuteNonQuery();

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }


        public void Delete(int id)
        {
            DataTable dt = new DataTable();
            try
            {
                connection.Open();
                SqlCommand cmd = new SqlCommand($"delete from  test  where id ={id}", connection);
                cmd.ExecuteNonQuery();

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }

    }
}

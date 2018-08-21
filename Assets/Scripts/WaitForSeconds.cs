namespace UTJ {

struct WaitForSeconds
{
	private float period_;
	private double start_;
	public WaitForSeconds(float period, double update_time)
	{
		period_ = period;
		start_ = update_time;
	}
	public bool end(double update_time)
	{
		return update_time - start_ > period_;
	}
}

} // namespace UTJ {
